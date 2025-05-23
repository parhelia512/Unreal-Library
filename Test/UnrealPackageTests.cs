using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UELib;
using UELib.Core;

namespace Eliot.UELib.Test
{
    [TestClass]
    public class UnrealPackageTests
    {
        public class MyUModel : UModel;
        
        [TestMethod]
        public void TestClassTypeOverride()
        {
            using var stream = UnrealPackageUtilities.CreateTempPackageStream();
            using var linker = new UnrealPackage(stream);
            
            Assert.IsTrue(linker.GetClassType("Model") == typeof(UnknownObject));
            linker.AddClassType("Model", typeof(MyUModel));
            Assert.IsTrue(linker.GetClassType("Model") == typeof(MyUModel));
            linker.InitializePackage(UnrealPackage.InitFlags.RegisterClasses);
            Assert.IsTrue(linker.GetClassType("Model") == typeof(UModel));

            using var stream2 = UnrealPackageUtilities.CreateTempPackageStream();
            using var linker2 = new UnrealPackage(stream2);

            // Swapped order...
            Assert.IsTrue(linker2.GetClassType("Model") == typeof(UnknownObject));
            linker2.InitializePackage(UnrealPackage.InitFlags.RegisterClasses);
            Assert.IsTrue(linker2.GetClassType("Model") == typeof(UModel));
            linker2.AddClassType("Model", typeof(MyUModel));
            Assert.IsTrue(linker2.GetClassType("Model") == typeof(MyUModel));
        }
        
        internal static void AssertTestClass(UnrealPackage linker)
        {
            var testClass = linker.FindObject<UClass>("Test");
            Assert.IsNotNull(testClass);

            // Validate that Public/Protected/Private are correct and distinguishable.
            var publicProperty = linker.FindObject<UIntProperty>("Public");
            Assert.IsNotNull(publicProperty);
            Assert.IsTrue(publicProperty.IsPublic());
            Assert.IsFalse(publicProperty.IsProtected());
            Assert.IsFalse(publicProperty.IsPrivate());

            var protectedProperty = linker.FindObject<UIntProperty>("Protected");
            Assert.IsNotNull(protectedProperty);
            Assert.IsTrue(protectedProperty.IsPublic());
            Assert.IsTrue(protectedProperty.IsProtected());
            Assert.IsFalse(protectedProperty.IsPrivate());

            var privateProperty = linker.FindObject<UIntProperty>("Private");
            Assert.IsNotNull(privateProperty);
            Assert.IsFalse(privateProperty.IsPublic());
            Assert.IsFalse(privateProperty.IsProtected());
            Assert.IsTrue(privateProperty.IsPrivate());
        }

        internal static void AssertScriptDecompile(UStruct scriptInstance)
        {
            if (scriptInstance.ByteCodeManager != null)
            {
                try
                {
                    scriptInstance.ByteCodeManager.Decompile();
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Token decompilation exception in script instance {scriptInstance.GetReferencePath()}: {ex.Message}");
                }
            }
            
            foreach (var subScriptInstance in scriptInstance
                         .EnumerateFields()
                         .OfType<UStruct>())
            {
                if (subScriptInstance.ByteCodeManager == null) continue;
                
                try
                {
                    subScriptInstance.ByteCodeManager.Decompile();
                    // ... for states
                    AssertScriptDecompile(subScriptInstance);
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Token decompilation exception in script instance {subScriptInstance.GetReferencePath()}: {ex.Message}");
                }
            }
        }

        internal static UObject AssertDefaultPropertiesClass(UnrealPackage linker)
        {
            var testClass = linker.FindObject<UClass>("DefaultProperties");
            Assert.IsNotNull(testClass);

            var defaults = testClass.Default ?? testClass;
            defaults.Load();
            Assert.IsNotNull(defaults.Properties);

            return defaults;
        }

        internal static void AssertPropertyTagFormat(UObject obj, string tagName, string expectedFormat)
        {
            Assert.IsNotNull(obj.Properties);
            var tag = obj.Properties.Find(tagName);
            Assert.IsNotNull(tag, $"Couldn't find property tag of '{tagName}'");
            string colorValue = tag.DeserializeValue();
            Assert.AreEqual(expectedFormat, colorValue, $"tag '{tagName}'");
        }

        internal static void AssertExportsOfType<T>(IEnumerable<UObject> objects)
            where T : UObject
        {
            var textures = objects.OfType<T>()
                .ToList();
            Assert.IsTrue(textures.Any());
            textures.ForEach(AssertObjectDeserialization);
        }
        
        internal static void AssertExports(IEnumerable<UObject> objects)
        {
            var compatibleExports = objects.Where(exp => exp is not UnknownObject)
                .ToList();
            Assert.IsTrue(compatibleExports.Any());
            compatibleExports.ForEach(AssertObjectDeserialization);
        }
        
        internal static void AssertObjectDeserialization(UObject obj)
        {
            if (obj.DeserializationState == 0)
            {
                obj.Load();
            }

            Assert.IsTrue(obj.DeserializationState == UObject.ObjectState.Deserialized, obj.GetReferencePath());
        }

        internal static void AssertTokenType<T>(UStruct.UByteCodeDecompiler.Token token)
            where T : UStruct.UByteCodeDecompiler.Token
        {
            Assert.AreEqual(typeof(T), token.GetType());
        }

        internal static void AssertTokenType(UStruct.UByteCodeDecompiler.Token token, Type tokenType)
        {
            Assert.AreEqual(tokenType, token.GetType());
        }

        internal static void AssertTokens(UStruct.UByteCodeDecompiler script, params Type[] tokenTypesSequence)
        {
            foreach (var tokenType in tokenTypesSequence)
            {
                AssertTokenType(script.NextToken, tokenType);
            }
        }
    }
}
