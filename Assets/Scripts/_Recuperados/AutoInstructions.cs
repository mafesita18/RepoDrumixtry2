// Placeholder FUNCIONAL hasta que llegue el original del repo (mismos campos y GUID que usa GestorInstrucciones).
// Cuando llegue el original: borra ESTE .cs y su .meta y pon el original CON su .meta.
using System.Collections;
using UnityEngine;

public class AutoInstructions : MonoBehaviour
{
    public GameObject instructionsUI;
    public float displayTime = 5f;

    IEnumerator Start()
    {
        if (!instructionsUI) yield break;
        instructionsUI.SetActive(true);
        if (displayTime <= 0f) yield break; // 0 = se queda visible
        yield return new WaitForSeconds(displayTime);
        if (instructionsUI) instructionsUI.SetActive(false);
    }

    /// Para un botón "Ayuda": vuelve a mostrar las instrucciones.
    public void Mostrar()
    {
        StopAllCoroutines();
        StartCoroutine(Start());
    }
}
