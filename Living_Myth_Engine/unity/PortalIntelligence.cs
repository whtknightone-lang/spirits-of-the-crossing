using UnityEngine;

public class PortalIntelligence : MonoBehaviour
{
    public MythEngine mythEngine;

    public string GetDestination()
    {
        if (mythEngine.HasMyth("forest"))
            return "VerdantWorld";

        if (mythEngine.HasMyth("machine"))
            return "MachineWorld";

        return "UnknownRealm";
    }
}
