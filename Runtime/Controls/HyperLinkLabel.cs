using System.Collections.Generic;
using System.Text.RegularExpressions;
using Plugins.Sim.Faciem.Shared;
using R3;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sim.Faciem.Controls
{
    [UxmlElement]
    public partial class HyperLinkLabel : Label
    {
        private const string LinkCursorClassName = "link-cursor";

        [UxmlAttribute, CreateProperty]
        public List<string> LinkIds { get; set; }
        
        [UxmlAttribute, CreateProperty]
        public List<int> InstanceIds { get; set; }
        
        [UxmlAttribute, CreateProperty]
        public Color LinkColor { get; set; }
        
        [UxmlAttribute, CreateProperty]
        public Color LinkHoverColor { get; set; }
        
        private bool _isSelfUpdate = false;
        
        private string _baseText;
        
        public HyperLinkLabel()
        {
            var disposables = this.RegisterDisposableBag();
            disposables.Add(this.AttachToPanelAsObservable()
                .Subscribe(_ => UpdateText(text)));
            disposables.Add(this.PointerDownAsObservable()
                .Subscribe(_ => { }));
            disposables.Add(this.ObserveEvent<PointerDownLinkTagEvent>()
                .Subscribe(_=> {}));
            disposables.Add(this.ObserveEvent<PointerUpLinkTagEvent>()
                .Subscribe(HyperlinkOnPointerUp));
            disposables.Add(this.ObserveEvent<PointerOverLinkTagEvent>()
                .Subscribe(HyperlinkOnPointerOver));
            disposables.Add(this.ObserveEvent<PointerOutLinkTagEvent>()
                .Subscribe(HyperlinkOnPointerOut));
            disposables.Add(this.ObserveEvent<PointerMoveLinkTagEvent>()
                .Subscribe(_ => { }));

            disposables.Add(this.ObserveValueChanged()
                .Where(_ => !_isSelfUpdate)
                .Subscribe(UpdateText));
        }

        private void UpdateText(string newText)
        {
            _isSelfUpdate = true;
            
            _baseText = Process_Base(newText, LinkColor);
            text = _baseText;
            
            schedule.Execute(() => _isSelfUpdate = false)
                .ExecuteLater(100);
        }

        private void HyperlinkOnPointerOver(PointerOverLinkTagEvent linkTagEvent)
        {
            AddToClassList(LinkCursorClassName);
            _isSelfUpdate = true;
            var hoverText = Process_Hover(linkTagEvent.linkText, LinkHoverColor);
            text = _baseText.Replace(linkTagEvent.linkText, hoverText);
            schedule.Execute(() => _isSelfUpdate = false)
                .ExecuteLater(100);
        }

        private void HyperlinkOnPointerOut(PointerOutLinkTagEvent linkTagEvent)
        {
            RemoveFromClassList(LinkCursorClassName);
            _isSelfUpdate = true;
            text = _baseText;
            schedule.Execute(() => _isSelfUpdate = false)
                .ExecuteLater(100);
        }

        private void HyperlinkOnPointerUp(PointerUpLinkTagEvent evt)
        {
            var index = LinkIds?.IndexOf(evt.linkID) ?? -1;

            if (index == -1)
            {
                return;
            }
            
            #if UNITY_EDITOR

            if (InstanceIds.Count > index)
            {
                var asset = EditorUtility.InstanceIDToObject(InstanceIds[index]);
                if (asset != null)
                {
                    EditorGUIUtility.PingObject(asset);
                }
            }
            
            #endif
        }

        private string Process_Base(string input, Color normalColor)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            string colorHex = ColorUtility.ToHtmlStringRGB(normalColor);

            return Regex.Replace(
                input,
                @"<link=""([^""]+)"">(.*?)</link>",
                m =>
                {
                    string id = m.Groups[1].Value;
                    string text = m.Groups[2].Value;

                    return $"<link=\"{id}\"><color=#{colorHex}>{text}</color></link>";
                },
                RegexOptions.Singleline
            );
        }
        
        private string Process_Hover(string input, Color hoverColor)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            string hoverHex = ColorUtility.ToHtmlStringRGB(hoverColor);

            return $"<color=#{hoverHex}><u>{input}</u></color>";
        }
    }
}