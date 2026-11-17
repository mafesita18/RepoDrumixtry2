using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Drumix
{
    /// Micro-feedback de botones: color al pasar/presionar, rebote, sonido y vibración leve.
    /// Funciona con mouse, XRI y el puntero VR de respaldo (todos mandan eventos de UI).
    [RequireComponent(typeof(Graphic))]
    public class DrumixButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public Color normal = new Color(0.38f, 0.16f, 0.62f);
        public Color hover = new Color(0.55f, 0.27f, 0.85f);
        public Color pressed = new Color(1f, 0.78f, 0.28f);
        public float hoverScale = 1.06f;
        [Tooltip("Bloqueado: se ve apagado y no reacciona (p. ej. nivel sin desbloquear).")]
        public bool locked;
        public Color lockedColor = new Color(0.16f, 0.11f, 0.22f);
        [Tooltip("Elegido: se queda con el color de 'hover' (p. ej. nivel elegido).")]
        public bool selected;
        Graphic graphic;
        Vector3 baseScale;
        bool isHover, isDown;
        float bounce = 10f;

        void Awake() { graphic = GetComponent<Graphic>(); baseScale = transform.localScale; if (graphic) graphic.color = normal; }
        void OnDisable() { isHover = isDown = false; transform.localScale = baseScale; if (graphic) graphic.color = normal; }

        public void OnPointerEnter(PointerEventData e) { isHover = true; if (!locked) DrumixUI.PlayUISound(false); }
        public void OnPointerExit(PointerEventData e) { isHover = false; isDown = false; }
        public void OnPointerDown(PointerEventData e) { isDown = true; }
        public void OnPointerUp(PointerEventData e) { isDown = false; }
        public void OnPointerClick(PointerEventData e) { if (locked) return; bounce = 0f; DrumixUI.PlayUISound(true); }

        void Update()
        {
            float k = 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime);
            bool live = !locked;
            Color target = !live ? lockedColor : isDown ? pressed : (isHover || selected) ? hover : normal;
            if (graphic) graphic.color = Color.Lerp(graphic.color, target, k);
            bounce += Time.unscaledDeltaTime;
            float b = bounce < 0.3f ? Mathf.Sin(bounce / 0.3f * Mathf.PI * 2f) * Mathf.Exp(-bounce * 12f) * 0.12f : 0f;
            float s = (!live ? 1f : isDown ? 0.94f : isHover ? hoverScale : 1f) + b;
            transform.localScale = Vector3.Lerp(transform.localScale, baseScale * s, k);
        }
    }
}
