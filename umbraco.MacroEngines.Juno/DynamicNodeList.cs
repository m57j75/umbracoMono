﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Dynamic;
using umbraco.interfaces;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Web.Compilation;
using System.Linq.Expressions;
using System.Linq.Dynamic;
namespace umbraco.MacroEngines
{
    public class DynamicNodeList : DynamicObject, IEnumerable
    {
        public IEnumerable<DynamicNode> Items { get; set; }

        public DynamicNodeList()
        {
            Items = new List<DynamicNode>();
        }
        public DynamicNodeList(IEnumerable<DynamicNode> items)
        {
            Items = items.ToList();
        }
        public DynamicNodeList(IEnumerable<INode> items)
        {
            Items = items.Select(x => new DynamicNode(x)).ToList();
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            var name = binder.Name;
            if (name == "Where")
            {
                string predicate = args.First().ToString();
                var values = args.Skip(1).ToArray();
                result = new DynamicNodeList(this.Where<DynamicNode>(predicate, values).ToList());
                return true;
            }
            if (name == "OrderBy")
            {
                result = new DynamicNodeList(this.OrderBy<DynamicNode>(args.First().ToString()).ToList());
                return true;
            }
            try
            {
                //Property?
                result = Items.GetType().InvokeMember(binder.Name,
                                                  System.Reflection.BindingFlags.Instance |
                                                  System.Reflection.BindingFlags.Public |
                                                  System.Reflection.BindingFlags.NonPublic |
                                                  System.Reflection.BindingFlags.GetProperty,
                                                  null,
                                                  Items,
                                                  args);
                return true;
            }
            catch (MissingMethodException)
            {
                try
                {
                    //Static or Instance Method?
                    result = Items.GetType().InvokeMember(binder.Name,
                                                  System.Reflection.BindingFlags.Instance |
                                                  System.Reflection.BindingFlags.Public |
                                                  System.Reflection.BindingFlags.NonPublic |
                                                  System.Reflection.BindingFlags.Static |
                                                  System.Reflection.BindingFlags.InvokeMethod,
                                                  null,
                                                  Items,
                                                  args);
                    return true;
                }
                catch (MissingMethodException)
                {

                    try
                    {
                        result = ExecuteExtensionMethod(args, name);
                        return true;
                    }
                    catch (TargetInvocationException)
                    {
                        //We do this to enable error checking of Razor Syntax when a method e.g. ElementAt(2) is used.
                        //When the Script is tested, there's no Children which means ElementAt(2) is invalid (IndexOutOfRange)
                        //Instead, we are going to return an empty DynamicNode.
                        result = new DynamicNode();
                        return true;
                    }

                    catch
                    {
                        result = null;
                        return false;
                    }

                }


            }
            catch
            {
                result = null;
                return false;
            }

        }
        List<MethodInfo> GetAllExtensionMethods(Type[] genericParameterTypeList, Type explicitTypeToSearch, string name, int argumentCount)
        {
            //get extension methods from runtime
            var candidates = (
                from assembly in BuildManager.GetReferencedAssemblies().Cast<Assembly>()
                where assembly.IsDefined(typeof(ExtensionAttribute), false)
                from type in assembly.GetTypes()
                where (type.IsDefined(typeof(ExtensionAttribute), false)
                    && type.IsSealed && !type.IsGenericType && !type.IsNested)
                from method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                // this filters extension methods
                where method.IsDefined(typeof(ExtensionAttribute), false)
                select method
                );

            //search an explicit type (e.g. Enumerable, where most of the Linq methods are defined)
            if (explicitTypeToSearch != null)
            {
                candidates = candidates.Concat(explicitTypeToSearch.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
            }

            //filter by name
            var methodsByName = candidates.Where(m => m.Name == name);

            var isGenericAndRightParamCount = methodsByName.Where(m => m.GetParameters().Length == argumentCount + 1);

            //find the right overload that can take genericParameterType
            //which will be either DynamicNodeList or List<DynamicNode> which is IEnumerable`

            var withGenericParameterType = isGenericAndRightParamCount.Select(m => new { m, t = firstParameterType(m) });

            var methodsWhereArgZeroIsTargetType = (from method in withGenericParameterType
                                                   where
                                                   method.t != null && methodArgZeroHasCorrectTargetType(method.m, method.t, genericParameterTypeList)
                                                   select method);

            return methodsWhereArgZeroIsTargetType.Select(mt => mt.m).ToList();
        }
        private bool methodArgZeroHasCorrectTargetType(MethodInfo method, Type firstArgumentType, Type[] genericParameterTypeList)
        {
            //This is done with seperate method calls because you can't debug/watch lamdas - if you're trying to figure
            //out why the wrong method is returned, it helps to be able to see each boolean result

            return

            // is it defined on me?
            methodArgZeroHasCorrectTargetType_TypeMatchesExactly(method, firstArgumentType, genericParameterTypeList) ||

            // or on any of my interfaces?
           methodArgZeroHasCorrectTargetType_AnInterfaceMatches(method, firstArgumentType, genericParameterTypeList) ||

            // or on any of my base types?
            methodArgZeroHasCorrectTargetType_IsASubclassOf(method, firstArgumentType, genericParameterTypeList) ||

           //share a common interface (e.g. IEnumerable)
            methodArgZeroHasCorrectTargetType_ShareACommonInterface(method, firstArgumentType, genericParameterTypeList);


        }

        private static bool methodArgZeroHasCorrectTargetType_ShareACommonInterface(MethodInfo method, Type firstArgumentType, Type[] genericParameterTypeList)
        {
            Type[] interfaces = firstArgumentType.GetInterfaces();
            if (interfaces.Length == 0)
            {
                return false;
            }
            bool result = interfaces.All(i => genericParameterTypeList.Any(gt => gt.GetInterfaces().Contains(i)));
            return result;
        }

        private static bool methodArgZeroHasCorrectTargetType_IsASubclassOf(MethodInfo method, Type firstArgumentType, Type[] genericParameterTypeList)
        {
            bool result = genericParameterTypeList.Any(gt => gt.IsSubclassOf(firstArgumentType));
            return result;
        }

        private static bool methodArgZeroHasCorrectTargetType_AnInterfaceMatches(MethodInfo method, Type firstArgumentType, Type[] genericParameterTypeList)
        {
            bool result = genericParameterTypeList.Any(gt => gt.GetInterfaces().Contains(firstArgumentType));
            return result;
        }

        private static bool methodArgZeroHasCorrectTargetType_TypeMatchesExactly(MethodInfo method, Type firstArgumentType, Type[] genericParameterTypeList)
        {
            bool result = genericParameterTypeList.Any(gt => gt == firstArgumentType);
            return result;
        }
        private Type firstParameterType(MethodInfo m)
        {
            ParameterInfo[] p = m.GetParameters();
            if (p.Count() > 0)
            {
                return p.First().ParameterType;
            }
            return null;
        }
        private object ExecuteExtensionMethod(object[] args, string name)
        {
            object result;
            //Extension method
            Type tObject = Items.GetType();
            Type t = tObject.GetGenericArguments()[0];

            var methods = GetAllExtensionMethods(new Type[] { typeof(DynamicNodeList), tObject }, typeof(Enumerable), name, args.Length);

            if (methods.Count == 0)
            {
                throw new MissingMethodException();
            }

            MethodInfo firstMethod = methods.First();
            // NH: this is to ensure that it's always the correct one being chosen when using the LINQ extension methods
            if (methods.Count > 1)
                firstMethod = methods.First(x => x.IsGenericMethodDefinition);

            MethodInfo methodToExecute = null;
            if (firstMethod.IsGenericMethodDefinition)
            {
                methodToExecute = firstMethod.MakeGenericMethod(t);
            }
            else
            {
                methodToExecute = firstMethod;
            }
            if (methodToExecute.GetParameters().First().ParameterType == typeof(DynamicNodeList))
            {
                var genericArgs = (new[] { this }).Concat(args);
                result = methodToExecute.Invoke(null, genericArgs.ToArray());
            }
            else
            {
                var genericArgs = (new[] { Items }).Concat(args);
                result = methodToExecute.Invoke(null, genericArgs.ToArray());
            }

            if (result is IEnumerable<INode>)
            {
                result = new DynamicNodeList((IEnumerable<INode>)result);
            }
            if (result is IEnumerable<DynamicNode>)
            {
                result = new DynamicNodeList((IEnumerable<DynamicNode>)result);
            }
            if (result is INode)
            {
                result = new DynamicNode((INode)result);
            }
            return result;
        }

        public IEnumerator GetEnumerator()
        {
            return Items.GetEnumerator();
        }

        public IQueryable<T> Where<T>(string predicate, params object[] values)
        {
            return ((IQueryable<T>)Items.AsQueryable()).Where(predicate, values);
        }
        public IQueryable<T> OrderBy<T>(string key)
        {
            return ((IQueryable<T>)Items.AsQueryable()).OrderBy(key);
        }
    }
}