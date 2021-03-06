﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Compilation.ControlTree.Resolved;

namespace DotVVM.Framework.Compilation.Validation
{
    public class DefaultControlUsageValidator : IControlUsageValidator
    {
        public static ConcurrentDictionary<Type, MethodInfo[]> cache = new ConcurrentDictionary<Type, MethodInfo[]>();
        public IEnumerable<ControlUsageError> Validate(IAbstractControl control)
        {
            var type = GetControlType(control.Metadata);
            if (type == null) return null;

            var result = new List<ControlUsageError>();
            var methods = cache.GetOrAdd(type, FindMethods);
            foreach (var method in methods)
            {
                var par = method.GetParameters();
                var args = new object[par.Length];
                for (int i = 0; i < par.Length; i++)
                {
                    if (par[i].ParameterType.IsAssignableFrom(control.GetType()))
                    {
                        args[i] = control;
                    }
                    else if (control.DothtmlNode != null && par[i].ParameterType.IsAssignableFrom(control.DothtmlNode.GetType()))
                    {
                        args[i] = control.DothtmlNode;
                    }
                    else
                    {
                        goto Error; // I think it is better that throw exceptions and catch them
                    }
                }
                var r = method.Invoke(null, args);
                if (r is IEnumerable<ControlUsageError>)
                {
                    result.AddRange((IEnumerable<ControlUsageError>)r);
                }
                else if (r is IEnumerable<string>)
                {
                    result.AddRange((r as IEnumerable<string>).Select(e => new ControlUsageError(e)));
                }
                continue;
                Error:;
            }

            return result
                // add current node to the error, if no control is specified
                .Select(e => e.Nodes.Length == 0 ?
                    new ControlUsageError(e.ErrorMessage, control.DothtmlNode) :
                    e);
        }

        protected virtual MethodInfo[] FindMethods(Type type)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(m => m.IsDefined(typeof(ControlUsageValidatorAttribute)))
                .ToArray();
            if (methods.Length > 0) return methods;
            else if (type == typeof(object)) return new MethodInfo[0];
            else return FindMethods(type.GetTypeInfo().BaseType);
        }

        protected virtual Type GetControlType(IControlResolverMetadata metadata)
        {
            var type = metadata.Type as ResolvedTypeDescriptor;
            return type?.Type;
        }
    }
}
