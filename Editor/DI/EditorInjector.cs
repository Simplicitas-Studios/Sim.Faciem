using System;
using System.Collections.Generic;
using System.Linq;
using Sim.Faciem;
using UnityEditor;

namespace Plugins.Sim.Faciem.Editor.DI
{
    [InitializeOnLoad]
    public class EditorInjector : IEditorInjector
    {
        public static IEditorInjector Instance { get; private set; }

        private static readonly Dictionary<Type, object> s_instances = new();
        private static readonly Dictionary<Type, ServiceRegistration> s_registeredTypes = new();

        static EditorInjector()
        {
            var editor = new EditorInjector();
            editor.RegisterInstance<IEditorInjector, EditorInjector>(editor);
            editor.Register<IDIContainerBridge, EditorInjector>();
            Instance = editor;
            
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            
            AutoRegisterEditorInstallers();
            AutoRegisterEditorViewIds(editor);
            
            InstantiateNonLazyServices();
        }
        
        private static void OnBeforeAssemblyReload()
        {
            foreach (IDisposable disposableService in s_instances.Values.OfType<IDisposable>())
            {
                disposableService.Dispose();
            }
        }

        public void RegisterInstance<TInterface, TImplementation>(TImplementation instance)
            where TImplementation : TInterface
        {
            Register<TInterface, TImplementation>();
            s_instances.Add(typeof(TInterface), instance);
        }

        public void Register<TInterface, TImplementation>(bool nonLazy = false, params Type[] aliases) where TImplementation : TInterface
        {
            var serviceRegistration = ServiceRegistration.Singleton(typeof(TImplementation), nonLazy);
            s_registeredTypes[typeof(TInterface)] = serviceRegistration;
            foreach (Type alias in aliases ?? Array.Empty<Type>())
            {
                s_registeredTypes[alias] = serviceRegistration;
            }
        }

        private void RegisterTransient(Type tInterface, Type tImplementation)
        {
            s_registeredTypes[tInterface] = ServiceRegistration.Transient(tImplementation);
        }

        // ResolveVersion with generic

        public TInterface ResolveInstance<TInterface>() where TInterface : class
        {
            var interfaceType = typeof(TInterface);
            return (TInterface) ResolveInstance(interfaceType);
        }

        // Resolve an instance of the given type, creating it if necessary
        public object ResolveInstance(Type interfaceType)
        {
            // Check if a singleton instance already exists
            if (s_instances.TryGetValue(interfaceType, out var existingInstance))
            {
                return existingInstance;
            }
            
            // Check if the type has been registered
            if (!s_registeredTypes.TryGetValue(interfaceType, out var implementationType))
            {
                throw new InvalidOperationException($"No registration for type {interfaceType.FullName}");
            }
            
            if (s_instances.TryGetValue(implementationType.InstanceType, out existingInstance))
            {
                return existingInstance;
            }

            // Create an instance using the constructor with the most parameters (dependency injection)
            var constructor = implementationType.InstanceType.GetConstructors()[0];
            var parameterInfos = constructor.GetParameters();
            var parameters = new object[parameterInfos.Length];

            for (int i = 0; i < parameterInfos.Length; i++)
            {
                parameters[i] = Resolve(parameterInfos[i].ParameterType);
            }

            var instance = constructor.Invoke(parameters);

            // Cache the singleton instance
            s_instances[interfaceType] = instance;
            s_instances[implementationType.InstanceType] = instance;

            return instance;
        }

        // Internal resolve method for non-generic calls

        private static object Resolve(Type interfaceType)
        {
            // Check if a singleton instance already exists
            if (s_instances.TryGetValue(interfaceType, out var existingInstance))
            {
                return existingInstance;
            }
            

            // Check if the type has been registered
            if (!s_registeredTypes.TryGetValue(interfaceType, out var implementationType))
            {
                throw new InvalidOperationException($"No registration for type {interfaceType.FullName}");
            }
            
            if (s_instances.TryGetValue(implementationType.InstanceType, out existingInstance))
            {
                return existingInstance;
            }

            // Create an instance using the constructor with the most parameters
            var constructor = implementationType.InstanceType.GetConstructors()[0];
            var parameterInfos = constructor.GetParameters();
            var parameters = new object[parameterInfos.Length];

            for (int i = 0; i < parameterInfos.Length; i++)
            {
                parameters[i] = Resolve(parameterInfos[i].ParameterType);
            }

            var instance = constructor.Invoke(parameters);

            // Cache the singleton instance
            s_instances[interfaceType] = instance;
            s_instances[implementationType.InstanceType] = instance;

            return instance;
        }

        private static void AutoRegisterEditorInstallers()
        {
            var installers = AssetDatabase.FindAssets($"t:{nameof(EditorServiceInstaller)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<EditorServiceInstaller>)
                .Where(instance => instance != null);

            foreach (var installer in installers)
            {
                installer.Install(Instance);
            }
        }

        private static void AutoRegisterEditorViewIds(EditorInjector injector)
        {
            var viewIdAssets = AssetDatabase.FindAssets($"t:{nameof(EditorViewIdAsset)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<EditorViewIdAsset>)
                .Where(asset => asset != null);

            foreach (var viewId in viewIdAssets)
            {
                if (viewId.DataContext == null || viewId.ViewModel == null)
                {
                    continue;
                }
                
                injector.RegisterTransient(viewId.DataContext.Script.GetClass(), viewId.ViewModel.Script.GetClass());
            }
        }

        private static void InstantiateNonLazyServices()
        {
            var nonLazyRegistrations = s_registeredTypes
                .Select(pair => pair)
                .Where(pair => pair.Value.NonLazy);
            
            foreach (var pair in nonLazyRegistrations)
            {
                Instance.ResolveInstance(pair.Key);
            }
        }
    }
}