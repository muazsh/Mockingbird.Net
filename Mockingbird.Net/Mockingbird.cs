using System.Reflection.Emit;
using System.Reflection;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Metadata;

namespace MockingbirdDotNet
{
    public class Types
    {
        public Type InterfaceImplementation { get; private set; }
        public Type[] MethodsDelegates { get; private set; }
        public Types(Type pInterfaceImplementation, Type[] pMethodsDelegates) 
        {
            InterfaceImplementation = pInterfaceImplementation; 
            MethodsDelegates = pMethodsDelegates;
        }
    }
    public static class Mockingbird
    {
        public static Types Build(Type pType)
        {
            var assemblyName = new AssemblyName("DynamicAssembly" + pType.Name);
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicModule");
            var typeName = "Mock" + pType.Name;

            var methods = pType.GetMethods(BindingFlags.Instance | BindingFlags.Public).Where(method => !method.IsSpecialName).ToArray();
            var delegateTypes = new Type[methods.Length];
            for (int i = 0; i < methods.Length; i++)
            {
                delegateTypes[i] = typeof(int);
            }

            var typeBuilder = moduleBuilder.DefineType(typeName, TypeAttributes.Public);
            typeBuilder.AddInterfaceImplementation(pType);

            var properties = pType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            BuildProperties(typeBuilder, properties);

            BuildDelegates(moduleBuilder, methods, delegateTypes);

            ConstructorBuilder defaultConstructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
            defaultConstructorBuilder.GetILGenerator().Emit(OpCodes.Ldarg_0);
            defaultConstructorBuilder.GetILGenerator().Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
            defaultConstructorBuilder.GetILGenerator().Emit(OpCodes.Ret);

            ConstructorBuilder constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, delegateTypes);
            ILGenerator ctorILGenerator = constructorBuilder.GetILGenerator();
            for (int i = 0; i < delegateTypes.Length; i++)
            {
                BuildMethod(typeBuilder, ctorILGenerator, methods[i], delegateTypes[i], i + 1);
            }
            ctorILGenerator.Emit(OpCodes.Ret);

            return new Types(typeBuilder.CreateType(), delegateTypes);
        }

        private static void BuildProperties(TypeBuilder pTypeBuilder, IEnumerable<PropertyInfo> pProperties)
        {
            foreach (var property in pProperties)
            {
                BuildProperty(pTypeBuilder, property);
            }
        }

        private static void BuildDelegates(ModuleBuilder pModuleBuilder, IEnumerable<MethodInfo> pMethods, Type[] pDelegateTypes)
        {
            for (int i = 0; i < pMethods.Count(); i++)
            {
                BuildDelegate(pModuleBuilder, pMethods.ElementAt(i), ref pDelegateTypes[i]);
            }
        }

        private static void BuildMethod(TypeBuilder pTypeBuilder, ILGenerator pCtorILGenerator, MethodInfo pMethodInfo, Type pDelegateType, int pMethodIndex)
        {
            pCtorILGenerator.Emit(OpCodes.Ldarg_0);
            pCtorILGenerator.Emit(OpCodes.Ldarg, pMethodIndex);

            var delegateFieldBuilder = pTypeBuilder.DefineField(pMethodInfo.Name + "DelegateMember" + (pMethodIndex - 1), pDelegateType, FieldAttributes.Public);

            pCtorILGenerator.Emit(OpCodes.Stfld, delegateFieldBuilder);

            var methodBuilder = pTypeBuilder.DefineMethod(pMethodInfo.Name, MethodAttributes.Public | MethodAttributes.Virtual, pMethodInfo.ReturnType, pMethodInfo.GetParameters().Select(p => p.ParameterType).ToArray());
            var methodILGenerator = methodBuilder.GetILGenerator();
            methodILGenerator.Emit(OpCodes.Ldarg_0);
            methodILGenerator.Emit(OpCodes.Ldfld, delegateFieldBuilder);
            for (int i = 0; i < pMethodInfo.GetParameters().Length; i++)
                methodILGenerator.Emit(OpCodes.Ldarg, i + 1);
            methodILGenerator.Emit(OpCodes.Callvirt, pDelegateType.GetMethod("Invoke"));
            methodILGenerator.Emit(OpCodes.Ret);
        }

        private static void BuildDelegate(ModuleBuilder pModuleBuilder, MethodInfo pMethodInfo, ref Type pDelegateType)
        {
            var delegateBuilder = pModuleBuilder.DefineType(
                pMethodInfo.Name + "Delegate", TypeAttributes.Public | TypeAttributes.AutoClass | TypeAttributes.Sealed, typeof(MulticastDelegate));

            var constructor = delegateBuilder.DefineConstructor(
                MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public,
                CallingConventions.Standard, pMethodInfo.GetParameters().Select(p => p.ParameterType).ToArray());
            constructor.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            var invokeDelegateMethod = delegateBuilder.DefineMethod(
                "Invoke", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                pMethodInfo.ReturnType, pMethodInfo.GetParameters().Select(p => p.ParameterType).ToArray());
            invokeDelegateMethod.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

            var delegateType = delegateBuilder.CreateType();
            pDelegateType = delegateType;

        }

        private static void BuildProperty(TypeBuilder pTypeBuilder, PropertyInfo pProperty)
        {
            var fieldName = pProperty.Name + "_Field";

            var propertyBuilder = pTypeBuilder.DefineProperty(pProperty.Name, PropertyAttributes.None, pProperty.PropertyType, Type.EmptyTypes);
            var fieldBuilder = pTypeBuilder.DefineField(fieldName, pProperty.PropertyType, FieldAttributes.Private);

            var getSetAttributes = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Virtual;
            var getterBuilder = BuildGetter(pTypeBuilder, pProperty, fieldBuilder, getSetAttributes);
            var setterBuilder = BuildSetter(pTypeBuilder, pProperty, fieldBuilder, getSetAttributes);

            propertyBuilder.SetGetMethod(getterBuilder);
            propertyBuilder.SetSetMethod(setterBuilder);
        }

        private static MethodBuilder BuildGetter(TypeBuilder pTypeBuilder, PropertyInfo pProperty, FieldBuilder pFieldBuilder, MethodAttributes pAttributes)
        {
            var getterBuilder = pTypeBuilder.DefineMethod("get_" + pProperty.Name, pAttributes, pProperty.PropertyType, Type.EmptyTypes);
            var ilGenerator = getterBuilder.GetILGenerator();

            ilGenerator.Emit(OpCodes.Ldarg_0);
            if (Nullable.GetUnderlyingType(pProperty.PropertyType) == null)
            {
                var valueNotNull = ilGenerator.DefineLabel();
                ilGenerator.Emit(OpCodes.Ldfld, pFieldBuilder);
                ilGenerator.Emit(OpCodes.Dup);
                ilGenerator.Emit(OpCodes.Ldnull);
                ilGenerator.Emit(OpCodes.Ceq);
                ilGenerator.Emit(OpCodes.Brfalse_S, valueNotNull);
                ilGenerator.Emit(OpCodes.Ldstr, pProperty.Name + " is not set.");

                var invalidOperationException = typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) });
                ilGenerator.Emit(OpCodes.Newobj, invalidOperationException);
                ilGenerator.Emit(OpCodes.Throw);
                ilGenerator.MarkLabel(valueNotNull);
            }
            ilGenerator.Emit(OpCodes.Ret);

            return getterBuilder;
        }

        private static MethodBuilder BuildSetter(TypeBuilder pTypeBuilder, PropertyInfo pProperty, FieldBuilder pFieldBuilder, MethodAttributes pAttributes)
        {
            var setterBuilder = pTypeBuilder.DefineMethod("set_" + pProperty.Name, pAttributes, null, new Type[] { pProperty.PropertyType });
            var ilGenerator = setterBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0);
            if (Nullable.GetUnderlyingType(pProperty.PropertyType) == null)
            {
                var valueNotNull = ilGenerator.DefineLabel();
                ilGenerator.Emit(OpCodes.Ldarg_1);
                ilGenerator.Emit(OpCodes.Ldnull);
                ilGenerator.Emit(OpCodes.Ceq);
                ilGenerator.Emit(OpCodes.Brfalse_S, valueNotNull);
                ilGenerator.Emit(OpCodes.Ldstr, pProperty.Name);

                var argumentNullException = typeof(ArgumentNullException).GetConstructor(new[] { typeof(string) });
                ilGenerator.Emit(OpCodes.Newobj, argumentNullException);
                ilGenerator.Emit(OpCodes.Throw);

                ilGenerator.MarkLabel(valueNotNull);
            }
            ilGenerator.Emit(OpCodes.Ldarg_1);
            ilGenerator.Emit(OpCodes.Stfld, pFieldBuilder);
            ilGenerator.Emit(OpCodes.Ret);

            return setterBuilder;
        }
    }
}