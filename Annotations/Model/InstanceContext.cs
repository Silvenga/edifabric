﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EdiFabric.Annotations.Edi;
using EdiFabric.Annotations.Validation;

namespace EdiFabric.Annotations.Model
{
    public sealed class InstanceContext
    {
        public object Instance { get; private set; }
        public PropertyInfo Property { get; private set; }
        public InstanceContext Parent { get; private set; }

        public string GetId()
        {
            if (Property == null)
                return null;

            var ediAttr = Property.GetGenericType().GetCustomAttribute<EdiAttribute>();
            if (ediAttr == null)
                throw new Exception(string.Format("Property {0} is not annotated with {1}.", Property.Name,
                    typeof (EdiAttribute).Name));

            return ediAttr.Id;
        }

        public string GetDeclaringTypeId()
        {
            if (Property == null || Property.DeclaringType == null)
                return null;

            var ediAttr = Property.DeclaringType.GetCustomAttribute<EdiAttribute>();
            if (ediAttr == null)
                throw new Exception(string.Format("The type {0} declaring property {1} is not annotated with {2}.",
                    Property.DeclaringType.Name, Property.Name, typeof(EdiAttribute).Name));

            return ediAttr.Id;
        }

        public bool IsInstanceOfType<T>() where T : EdiAttribute
        {
            if (Instance == null)
                return false;

            var list = Instance as IList;

            if (list != null)
                return false;

            return IsPropertyOfType<T>();
        }

        public bool IsPropertyOfType<T>() where T : EdiAttribute
        {
            return Property != null && Property.GetGenericType().GetCustomAttribute<T>() != null;
        }

        public InstanceContext(object instance)
        {
            Instance = instance;
        }

        public InstanceContext(object instance, PropertyInfo property, InstanceContext parent)
            : this(instance)
        {
            Property = property;
            Parent = parent;
        }

        public List<SegmentErrorContext> Validate(int segmentIndex, int inSegmentIndex, int inComponentIndex)
        {
            var result = new List<SegmentErrorContext>();

            if (Property == null) return result;

            var validationAttributes = Property.GetCustomAttributes<ValidationAttribute>().OrderBy(a => a.Priority);
            foreach (var validationAttribute in validationAttributes)
            {
                result.AddRange(validationAttribute.IsValid(this, segmentIndex, inSegmentIndex, inComponentIndex));
            }

            return result;
        }

        public IEnumerable<InstanceContext> GetNeigbours()
        {
            if (Instance != null && !(Instance is string))
            {
                var list = Instance as IList;
                if (list != null)
                {
                    foreach (var listValue in list)
                    {
                        yield return
                            new InstanceContext(listValue, Property, this);
                    }
                }
                else
                {
                    foreach (
                        var propertyInfo in
                            Instance.GetType()
                                .GetProperties(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance)
                                .Sort())
                    {
                        yield return new InstanceContext(propertyInfo.GetValue(Instance), propertyInfo, this);
                    }
                }
            }

            if (Parent != null)
                yield return Parent;
        }

        public void SetIndexes(ref int segmentIndex, ref int inSegmentIndex, ref int inComponentIndex)
        {
            if (IsInstanceOfType<SegmentAttribute>())
            {
                segmentIndex++;
                inSegmentIndex = 0;
                inComponentIndex = 0;
            }

            if (Parent != null && Parent.IsInstanceOfType<SegmentAttribute>())
            {
                inSegmentIndex++;
                inComponentIndex = 0;
            }

            if (Parent != null && Parent.IsInstanceOfType<CompositeAttribute>())
            {
                inComponentIndex++;
            }
        }
    }
}