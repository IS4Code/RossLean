﻿namespace RossLean.DynamicGenericBridge
{
    partial class Execution
    {
        public const string DynamicBridgeAttribute = "RossLean.DynamicBridgeAttribute";
        public const string DynamicBridgeMethodAttribute = "RossLean.DynamicBridgeMethodAttribute";

        const string DynamicBridgeMethodAttributeAttributes = "Attributes";
        const string DynamicBridgeMethodAttributeIgnoreUnbound = "IgnoreUnbound";
    }

    partial class Generator
    {
        const string AttributeDefinition = @"#nullable enable

using System;
using System.Diagnostics;

namespace RossLean
{
    [AttributeUsage(AttributeTargets.GenericParameter, Inherited = false, AllowMultiple = false)]
    [Conditional(""DYNAMIC_GENERIC_BRIDGE_ATTRIBUTES"")]
    internal sealed class DynamicBridgeAttribute : Attribute
    {
        public Type? LoweredType { get; }

        public DynamicBridgeAttribute()
        {
        }

        public DynamicBridgeAttribute(Type loweredType)
        {
            LoweredType = loweredType;
        }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    [Conditional(""DYNAMIC_GENERIC_BRIDGE_ATTRIBUTES"")]
    internal sealed class DynamicBridgeMethodAttribute : Attribute
    {
        public string? MethodName { get; }
        public string? Attributes { get; set; }
        public bool IgnoreUnbound { get; set; }

        public DynamicBridgeMethodAttribute()
        {
        }

        public DynamicBridgeMethodAttribute(string methodName)
        {
            MethodName = methodName;
        }
    }
}
";

    }

    partial class Execution
    {
        const string DynamicDependencyAttribute = "System.Diagnostics.CodeAnalysis.DynamicDependencyAttribute";
        const string DynamicallyAccessedMemberTypes = "System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes";
        const string UnconditionalSuppressMessageAttribute = "System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute";

        const string DynamicDependencyAttributeDefinition = @"#nullable enable

// https://github.com/dotnet/runtime/blob/527f9ae88a0ee216b44d556f9bdc84037fe0ebda/src/libraries/System.Private.CoreLib/src/System/Diagnostics/CodeAnalysis/DynamicDependencyAttribute.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// States a dependency that one member has on another.
    /// </summary>
    /// <remarks>
    /// This can be used to inform tooling of a dependency that is otherwise not evident purely from
    /// metadata and IL, for example a member relied on via reflection.
    /// </remarks>
    [AttributeUsage(
        AttributeTargets.Constructor | AttributeTargets.Field | AttributeTargets.Method,
        AllowMultiple = true, Inherited = false)]
#if SYSTEM_PRIVATE_CORELIB
    public
#else
    internal
#endif
    sealed class DynamicDependencyAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref=""DynamicDependencyAttribute""/> class
        /// with the specified signature of a member on the same type as the consumer.
        /// </summary>
        /// <param name=""memberSignature"">The signature of the member depended on.</param>
        public DynamicDependencyAttribute(string memberSignature)
        {
            MemberSignature = memberSignature;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref=""DynamicDependencyAttribute""/> class
        /// with the specified signature of a member on a <see cref=""System.Type""/>.
        /// </summary>
        /// <param name=""memberSignature"">The signature of the member depended on.</param>
        /// <param name=""type"">The <see cref=""System.Type""/> containing <paramref name=""memberSignature""/>.</param>
        public DynamicDependencyAttribute(string memberSignature, Type type)
        {
            MemberSignature = memberSignature;
            Type = type;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref=""DynamicDependencyAttribute""/> class
        /// with the specified signature of a member on a type in an assembly.
        /// </summary>
        /// <param name=""memberSignature"">The signature of the member depended on.</param>
        /// <param name=""typeName"">The full name of the type containing the specified member.</param>
        /// <param name=""assemblyName"">The assembly name of the type containing the specified member.</param>
        public DynamicDependencyAttribute(string memberSignature, string typeName, string assemblyName)
        {
            MemberSignature = memberSignature;
            TypeName = typeName;
            AssemblyName = assemblyName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref=""DynamicDependencyAttribute""/> class
        /// with the specified types of members on a <see cref=""System.Type""/>.
        /// </summary>
        /// <param name=""memberTypes"">The types of members depended on.</param>
        /// <param name=""type"">The <see cref=""System.Type""/> containing the specified members.</param>
        public DynamicDependencyAttribute(DynamicallyAccessedMemberTypes memberTypes, Type type)
        {
            MemberTypes = memberTypes;
            Type = type;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref=""DynamicDependencyAttribute""/> class
        /// with the specified types of members on a type in an assembly.
        /// </summary>
        /// <param name=""memberTypes"">The types of members depended on.</param>
        /// <param name=""typeName"">The full name of the type containing the specified members.</param>
        /// <param name=""assemblyName"">The assembly name of the type containing the specified members.</param>
        public DynamicDependencyAttribute(DynamicallyAccessedMemberTypes memberTypes, string typeName, string assemblyName)
        {
            MemberTypes = memberTypes;
            TypeName = typeName;
            AssemblyName = assemblyName;
        }

        /// <summary>
        /// Gets the signature of the member depended on.
        /// </summary>
        /// <remarks>
        /// Either <see cref=""MemberSignature""/> must be a valid string or <see cref=""MemberTypes""/>
        /// must not equal <see cref=""DynamicallyAccessedMemberTypes.None""/>, but not both.
        /// </remarks>
        public string? MemberSignature { get; }

        /// <summary>
        /// Gets the <see cref=""DynamicallyAccessedMemberTypes""/> which specifies the type
        /// of members depended on.
        /// </summary>
        /// <remarks>
        /// Either <see cref=""MemberSignature""/> must be a valid string or <see cref=""MemberTypes""/>
        /// must not equal <see cref=""DynamicallyAccessedMemberTypes.None""/>, but not both.
        /// </remarks>
        public DynamicallyAccessedMemberTypes MemberTypes { get; }

        /// <summary>
        /// Gets the <see cref=""System.Type""/> containing the specified member.
        /// </summary>
        /// <remarks>
        /// If neither <see cref=""Type""/> nor <see cref=""TypeName""/> are specified,
        /// the type of the consumer is assumed.
        /// </remarks>
        public Type? Type { get; }

        /// <summary>
        /// Gets the full name of the type containing the specified member.
        /// </summary>
        /// <remarks>
        /// If neither <see cref=""Type""/> nor <see cref=""TypeName""/> are specified,
        /// the type of the consumer is assumed.
        /// </remarks>
        public string? TypeName { get; }

        /// <summary>
        /// Gets the assembly name of the specified type.
        /// </summary>
        /// <remarks>
        /// <see cref=""AssemblyName""/> is only valid when <see cref=""TypeName""/> is specified.
        /// </remarks>
        public string? AssemblyName { get; }

        /// <summary>
        /// Gets or sets the condition in which the dependency is applicable, e.g. ""DEBUG"".
        /// </summary>
        public string? Condition { get; set; }
    }
}
";

        const string DynamicallyAccessedMemberTypesDefinition = @"#nullable enable
// https://github.com/dotnet/runtime/blob/527f9ae88a0ee216b44d556f9bdc84037fe0ebda/src/libraries/System.Private.CoreLib/src/System/Diagnostics/CodeAnalysis/DynamicallyAccessedMemberTypes.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Specifies the types of members that are dynamically accessed.
    ///
    /// This enumeration has a <see cref=""FlagsAttribute""/> attribute that allows a
    /// bitwise combination of its member values.
    /// </summary>
    [Flags]
#if SYSTEM_PRIVATE_CORELIB
    public
#else
    internal
#endif
    enum DynamicallyAccessedMemberTypes
    {
        /// <summary>
        /// Specifies no members.
        /// </summary>
        None = 0,

        /// <summary>
        /// Specifies the default, parameterless public constructor.
        /// </summary>
        PublicParameterlessConstructor = 0x0001,

        /// <summary>
        /// Specifies all public constructors.
        /// </summary>
        PublicConstructors = 0x0002 | PublicParameterlessConstructor,

        /// <summary>
        /// Specifies all non-public constructors.
        /// </summary>
        NonPublicConstructors = 0x0004,

        /// <summary>
        /// Specifies all public methods.
        /// </summary>
        PublicMethods = 0x0008,

        /// <summary>
        /// Specifies all non-public methods.
        /// </summary>
        NonPublicMethods = 0x0010,

        /// <summary>
        /// Specifies all public fields.
        /// </summary>
        PublicFields = 0x0020,

        /// <summary>
        /// Specifies all non-public fields.
        /// </summary>
        NonPublicFields = 0x0040,

        /// <summary>
        /// Specifies all public nested types.
        /// </summary>
        PublicNestedTypes = 0x0080,

        /// <summary>
        /// Specifies all non-public nested types.
        /// </summary>
        NonPublicNestedTypes = 0x0100,

        /// <summary>
        /// Specifies all public properties.
        /// </summary>
        PublicProperties = 0x0200,

        /// <summary>
        /// Specifies all non-public properties.
        /// </summary>
        NonPublicProperties = 0x0400,

        /// <summary>
        /// Specifies all public events.
        /// </summary>
        PublicEvents = 0x0800,

        /// <summary>
        /// Specifies all non-public events.
        /// </summary>
        NonPublicEvents = 0x1000,

        /// <summary>
        /// Specifies all members.
        /// </summary>
        All = ~None
    }
}
";

        const string UnconditionalSuppressMessageAttributeDefinition = @"#nullable enable
// https://raw.githubusercontent.com/dotnet/runtime/527f9ae88a0ee216b44d556f9bdc84037fe0ebda/src/libraries/System.Private.CoreLib/src/System/Diagnostics/CodeAnalysis/UnconditionalSuppressMessageAttribute.cs

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Suppresses reporting of a specific rule violation, allowing multiple suppressions on a
    /// single code artifact.
    /// </summary>
    /// <remarks>
    /// <see cref=""UnconditionalSuppressMessageAttribute""/> is different than
    /// <see cref=""SuppressMessageAttribute""/> in that it doesn't have a
    /// <see cref=""ConditionalAttribute""/>. So it is always preserved in the compiled assembly.
    /// </remarks>
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
#if SYSTEM_PRIVATE_CORELIB
    public
#else
    internal
#endif
    sealed class UnconditionalSuppressMessageAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref=""UnconditionalSuppressMessageAttribute""/>
        /// class, specifying the category of the tool and the identifier for an analysis rule.
        /// </summary>
        /// <param name=""category"">The category for the attribute.</param>
        /// <param name=""checkId"">The identifier of the analysis rule the attribute applies to.</param>
        public UnconditionalSuppressMessageAttribute(string category, string checkId)
        {
            Category = category;
            CheckId = checkId;
        }

        /// <summary>
        /// Gets the category identifying the classification of the attribute.
        /// </summary>
        /// <remarks>
        /// The <see cref=""Category""/> property describes the tool or tool analysis category
        /// for which a message suppression attribute applies.
        /// </remarks>
        public string Category { get; }

        /// <summary>
        /// Gets the identifier of the analysis tool rule to be suppressed.
        /// </summary>
        /// <remarks>
        /// Concatenated together, the <see cref=""Category""/> and <see cref=""CheckId""/>
        /// properties form a unique check identifier.
        /// </remarks>
        public string CheckId { get; }

        /// <summary>
        /// Gets or sets the scope of the code that is relevant for the attribute.
        /// </summary>
        /// <remarks>
        /// The Scope property is an optional argument that specifies the metadata scope for which
        /// the attribute is relevant.
        /// </remarks>
        public string? Scope { get; set; }

        /// <summary>
        /// Gets or sets a fully qualified path that represents the target of the attribute.
        /// </summary>
        /// <remarks>
        /// The <see cref=""Target""/> property is an optional argument identifying the analysis target
        /// of the attribute. An example value is ""System.IO.Stream.ctor():System.Void"".
        /// Because it is fully qualified, it can be long, particularly for targets such as parameters.
        /// The analysis tool user interface should be capable of automatically formatting the parameter.
        /// </remarks>
        public string? Target { get; set; }

        /// <summary>
        /// Gets or sets an optional argument expanding on exclusion criteria.
        /// </summary>
        /// <remarks>
        /// The <see cref=""MessageId ""/> property is an optional argument that specifies additional
        /// exclusion where the literal metadata target is not sufficiently precise. For example,
        /// the <see cref=""UnconditionalSuppressMessageAttribute""/> cannot be applied within a method,
        /// and it may be desirable to suppress a violation against a statement in the method that will
        /// give a rule violation, but not against all statements in the method.
        /// </remarks>
        public string? MessageId { get; set; }

        /// <summary>
        /// Gets or sets the justification for suppressing the code analysis message.
        /// </summary>
        public string? Justification { get; set; }
    }
}
";
    }
}
