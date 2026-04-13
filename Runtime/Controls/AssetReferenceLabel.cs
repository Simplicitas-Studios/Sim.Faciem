using Plugins.Sim.Faciem.Shared;
using Sim.Faciem;
using R3;
using Unity.Properties;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine.UIElements;

namespace Sim.Faciem.Controls
{
    [UxmlElement]
    public partial class AssetReferenceLabel : Label
    {
        private bool _didTryToResolve;
        private string _baseText;
        
        [UxmlAttribute, CreateProperty]
        public UnityEngine.Object LinkedAsset { get; set; }
        
        [UxmlAttribute, CreateProperty]
        public int InstanceId { get; set; }

        public AssetReferenceLabel()
        {
            var disposables = this.RegisterDisposableBag();
            
            disposables.Add(this.MouseEnterAsObservable()
                .Do(_ => CheckAsset())
                .Where(_ => LinkedAsset != null)
                .Subscribe(_ =>
                {
                    _baseText = text;
                    text = $"<u>{text}</u>";
                }));
            
            disposables.Add(this.MouseLeaveAsObservable()
                .Where(_ => LinkedAsset != null)
                .Subscribe(_ =>
                {
                    text = $"{_baseText}";
                }));
            
#if UNITY_EDITOR
            disposables.Add(
                this.MouseDownAsObservable()
                    .Subscribe(_ =>
                    {
                        if(LinkedAsset == null && !_didTryToResolve)
                        {
                            _didTryToResolve = true;
                            LinkedAsset = EditorUtility.InstanceIDToObject(InstanceId);
                        }
                        
                        if (LinkedAsset != null)
                        {
                            EditorGUIUtility.PingObject(LinkedAsset);
                        }
                    }));
#endif
        }

        private void CheckAsset()
        {
#if UNITY_EDITOR
            if(LinkedAsset == null && !_didTryToResolve)
            {
                _didTryToResolve = true;
                LinkedAsset = EditorUtility.InstanceIDToObject(InstanceId);
            }
#endif
        }
    }
}