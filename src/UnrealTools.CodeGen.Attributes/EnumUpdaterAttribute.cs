using CodeGeneration.Roslyn;
using System;
using System.Diagnostics;

namespace UnrealTools.CodeGen.Attributes
{
    /// <summary>
    /// Implements attribute triggering autogeneration of new enum from the UE4 documentation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum, Inherited = false, AllowMultiple = false)]
    [CodeGenerationAttribute("UnrealTools.CodeGen.EnumUpdaterCodeGen, UnrealTools.CodeGen, Version=" + ThisAssembly.AssemblyVersion + ", Culture=neutral, PublicKeyToken=" + ThisAssembly.PublicKeyToken)]
    [Conditional("CodeGeneration")]
    public sealed class EnumUpdaterAttribute : Attribute
    {
        /// <summary>
        /// Name for the generated enum.
        /// </summary>
        public string Name { get; set; }
        private EnumUpdaterAttribute() { }
        /// <summary>
        /// Triggers the code generation, parsing <paramref name="url"/> to actual enum.
        /// </summary>
        /// <param name="url">Url to documentation.</param>
        /// <remarks>
        /// Enum type must have '_Stub' suffix, and should have no declared members.
        /// Should specify generated enum base type if it's needed.
        /// </remarks>
        /// <example>
        /// [EnumUpdater("https://docs.unrealengine.com/en-US/API/Runtime/CoreUObject/UObject/EPackageFlags/index.html")]
        /// enum EPackageFlags_Stub : uint { }
        /// </example>
        public EnumUpdaterAttribute(string url) => _ = url;
    }
}
