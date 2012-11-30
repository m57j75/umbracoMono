﻿using System;
using System.Collections.Generic;
using Umbraco.Core;

namespace Umbraco.Tests.CodeFirst
{
    /// <summary>
    /// Used for PluginTypeResolverTests
    /// </summary>
    internal static class PluginManagerExtensions
    {
        public static IEnumerable<Type> ResolveContentTypeBaseTypes(this PluginManager resolver)
        {
            return resolver.ResolveTypes<ContentTypeBase>();
        }
    }
}