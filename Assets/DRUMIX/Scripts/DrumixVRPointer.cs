using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;

namespace Drumix
{
    /// Rayo para la UI en el rig de respaldo (sin prefab XRI). Gatillo = click.
    public class DrumixVRPointer : MonoBehaviour
    {
        public XRNode hand = XRNode.RightHand;
        public Canvas canvas;
        public LineRenderer line;
        public float maxDistance = 6f;
        readonly List<RaycastResult> hits = new List<RaycastResult>();
        PointerEventData pointer; EventSystem system;
        GameObject hovered, pressedObj;
        bool wasPressed;

        void Update()
        {
            if (!canvas) { var ui = DrumixUI.Instance; if (ui) canvas = ui.Canvas; }
            var device = InputDevices.GetDeviceAtXRNode(hand);
            bool pressed = false;
            if (device.isValid) device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out pressed);
            Vector3 end = transform.position + transform.forward * maxDistance;
            GameObject target = null;
            var raycaster = canvas ? canvas.GetComponent<GraphicRaycaster>() : null;
            if (EventSystem.current && (pointer == null || system != EventSystem.current)) { system = EventSystem.current; pointer = new PointerEventData(system); }
            if (device.isValid && canvas && canvas.isActiveAndEnabled && canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera && raycaster && pointer != null)
            {
                var plane = new Plane(canvas.transform.forward, canvas.transform.position);
                var ray = new Ray(transform.position, transform.forward);
                float d;
                if (plane.Raycast(ray, out d) && d <= maxDistance)
                {
                    end = ray.GetPoint(d);
                    pointer.position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, end);
                    hits.Clear(); raycaster.Raycast(pointer, hits);
                    if (hits.Count > 0) { target = hits[0].gameObject; pointer.pointerCurrentRaycast = hits[0]; }
                }
            }
            if (pointer != null)
            {
                GameObject enter = target ? ExecuteEvents.GetEventHandler<IPointerEnterHandler>(target) : null;
                if (enter != hovered)
                {
                    if (hovered) ExecuteEvents.Execute(hovered, pointer, ExecuteEvents.pointerExitHandler);
                    hovered = enter;
                    if (hovered) ExecuteEvents.Execute(hovered, pointer, ExecuteEvents.pointerEnterHandler);
                }
                if (pressed && !wasPressed && target)
                {
                    pressedObj = ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerDownHandler);
                    if (!pressedObj) pressedObj = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
                }
                if (!pressed && wasPressed && pressedObj)
                {
                    ExecuteEvents.Execute(pressedObj, pointer, ExecuteEvents.pointerUpHandler);
                    if (target && pressedObj == ExecuteEvents.GetEventHandler<IPointerClickHandler>(target))
                    {
                        ExecuteEvents.Execute(pressedObj, pointer, ExecuteEvents.pointerClickHandler);
                        if (device.isValid) device.SendHapticImpulse(0, 0.25f, 0.03f);
                    }
                    pressedObj = null;
                }
            }
            wasPressed = pressed;
            if (line)
            {
                bool show = device.isValid && canvas && canvas.isActiveAndEnabled;
                line.enabled = show;
                if (show) { line.SetPosition(0, transform.position); line.SetPosition(1, end); line.startColor = line.endColor = target ? new Color(1f, 0.8f, 0.3f) : new Color(0.7f, 0.5f, 1f, 0.6f); }
            }
        }
    }
}
