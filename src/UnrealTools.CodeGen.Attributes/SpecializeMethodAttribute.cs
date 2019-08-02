using CodeGeneration.Roslyn;
using System;
using System.Diagnostics;

namespace UnrealTools.CodeGen.Attributes
{
    /// <summary>
    /// Implements attribute triggering generation of specialized variant of generic method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    [CodeGenerationAttribute("UnrealTools.CodeGen.SpecializeMethodCodeGen, UnrealTools.CodeGen, Version=" + ThisAssembly.AssemblyVersion + ", Culture=neutral, PublicKeyToken=" + ThisAssembly.PublicKeyToken)]
    [Conditional("CodeGeneration")]
    public sealed class SpecializeMethodAttribute : Attribute
    {
        private SpecializeMethodAttribute() { }
        /// <summary>
        /// Initializes generation of specialized variant of generic method for specified <paramref name="types"/>.
        /// </summary>
        /// <param name="types">Types to generate specializations for.</param>
        public SpecializeMethodAttribute(params Type[] types) => _ = types;
    }
}
