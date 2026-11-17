// Placeholder FUNCIONAL hasta que llegue el original del repo (mismos campos serializados y mismo GUID que usan
// los SensorBowl de 1.NivelUno..3.NivelTres). Cuando llegue el original: borra ESTE .cs y su .meta y pon el original CON su .meta.
using UnityEngine;
using Drumix;

public class TargetZoneTrigger : MonoBehaviour
{
    public AudioClip successSound;
    public string targetObjectName = "";
    public AudioSource audioSource;
    [Tooltip("Pieza que representa este sensor (para el público/UI de DRUMIX).")]
    public DrumixLane lane = DrumixLane.Snare;
    [Tooltip("Si entra otra cosa que no es el objeto esperado, cuenta como fallo (no baja el %).")]
    public bool wrongObjectIsMiss = false;

    public int Aciertos { get; private set; }
    public int Fallos { get; private set; }

    void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        bool match = string.IsNullOrEmpty(targetObjectName) ||
                     other.name.IndexOf(targetObjectName, System.StringComparison.OrdinalIgnoreCase) >= 0;
        if (match)
        {
            Aciertos++;
            if (successSound)
            {
                var source = audioSource ? audioSource : GetComponent<AudioSource>();
                if (source) source.PlayOneShot(successSound);
                else AudioSource.PlayClipAtPoint(successSound, transform.position);
            }
            DrumixEvents.ReportHit(new DrumixHitInfo { lane = lane, judgement = DrumixJudgement.Good, combo = Aciertos, stray = true });
        }
        else if (wrongObjectIsMiss)
        {
            Fallos++;
            DrumixEvents.ReportMiss(new DrumixHitInfo { lane = lane, judgement = DrumixJudgement.Miss, stray = true });
        }
    }
}
