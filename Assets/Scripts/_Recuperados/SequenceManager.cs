// Placeholder FUNCIONAL hasta que llegue el original del repo. Las escenas del equipo llaman ValidarError() desde
// eventos de XR Grab. Cuando llegue el original: borra ESTE .cs y su .meta y pon el original CON su .meta.
using UnityEngine;
using Drumix;

public class SequenceManager : MonoBehaviour
{
    public int errores;
    public int aciertos;

    /// Registra un fallo: lo ven el público, la UI y los lanzamientos de DRUMIX (como fallo "suelto": no baja el %).
    public void ValidarError()
    {
        errores++;
        DrumixEvents.ReportMiss(new DrumixHitInfo { lane = DrumixLane.Snare, judgement = DrumixJudgement.Miss, stray = true });
    }

    /// Registra un acierto suelto (por si algún evento del equipo lo quiere usar).
    public void ValidarAcierto()
    {
        aciertos++;
        DrumixEvents.ReportHit(new DrumixHitInfo { lane = DrumixLane.Snare, judgement = DrumixJudgement.Good, combo = aciertos, stray = true });
    }
}
