using UnityEngine;

public enum SceneVertexGeneratedObjectKind
{
    GroundPlane,
    LayoutRoot
}

[DisallowMultipleComponent]
public sealed class SceneVertexGeneratedObjectMarker : MonoBehaviour
{
    [HideInInspector]
    public SceneVertexGeneratedObjectKind kind;
}
