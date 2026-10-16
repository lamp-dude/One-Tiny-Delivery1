using System.Collections.Generic;
using UnityEngine;

public enum AmbienceType
{
    Forest,
    Sea
}

[DisallowMultipleComponent]
public sealed class AmbienceZone : MonoBehaviour
{
    private static readonly HashSet<AmbienceZone> ActiveZoneSet = new HashSet<AmbienceZone>();

    [SerializeField] private AmbienceType ambienceType = AmbienceType.Forest;
    [SerializeField, Min(0f)] private float blendDistance = 20f;
    [SerializeField, Range(0f, 1f)] private float intensity = 1f;
    [SerializeField] private List<Collider> zoneColliders = new List<Collider>();

    public AmbienceType Type => ambienceType;
    public float BlendDistance => blendDistance;
    public float Intensity => intensity;
    public static IEnumerable<AmbienceZone> ActiveZones => ActiveZoneSet;

    private void OnEnable()
    {
        ActiveZoneSet.Add(this);
    }

    private void OnDisable()
    {
        ActiveZoneSet.Remove(this);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        ActiveZoneSet.Clear();
    }

    public float GetInfluence(Vector3 listenerPosition)
    {
        float strongestInfluence = 0f;

        for (int i = 0; i < zoneColliders.Count; i++)
        {
            Collider zoneCollider = zoneColliders[i];
            if (zoneCollider == null || !zoneCollider.enabled || !zoneCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 closestPoint = zoneCollider.ClosestPoint(listenerPosition);
            float distance = Vector3.Distance(listenerPosition, closestPoint);
            float influence = distance <= Mathf.Epsilon
                ? 1f
                : 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, blendDistance));

            // Smoothstep avoids a noticeable volume corner at the edge of the fade range.
            influence = influence * influence * (3f - 2f * influence);
            strongestInfluence = Mathf.Max(strongestInfluence, influence);
        }

        return strongestInfluence * intensity;
    }

    public void Configure(AmbienceType type, Collider zoneCollider, float fadeDistance, float volume)
    {
        ambienceType = type;
        blendDistance = Mathf.Max(0f, fadeDistance);
        intensity = Mathf.Clamp01(volume);
        zoneColliders.Clear();

        if (zoneCollider != null)
        {
            zoneCollider.isTrigger = true;
            zoneColliders.Add(zoneCollider);
        }
    }

    [ContextMenu("Collect Trigger Colliders In Children")]
    private void CollectTriggerCollidersInChildren()
    {
        zoneColliders.Clear();
        Collider[] colliders = GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].isTrigger)
            {
                zoneColliders.Add(colliders[i]);
            }
        }
    }

    private void OnValidate()
    {
        blendDistance = Mathf.Max(0f, blendDistance);
        intensity = Mathf.Clamp01(intensity);
    }

    private void OnDrawGizmos()
    {
        DrawZoneGizmos(false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawZoneGizmos(true);
    }

    private void DrawZoneGizmos(bool drawFill)
    {
        Color fillColor = ambienceType == AmbienceType.Sea
            ? new Color(0.1f, 0.65f, 1f, 0.18f)
            : new Color(0.2f, 0.85f, 0.35f, 0.18f);
        Color outlineColor = new Color(fillColor.r, fillColor.g, fillColor.b, 0.85f);

        for (int i = 0; i < zoneColliders.Count; i++)
        {
            Collider zoneCollider = zoneColliders[i];
            if (zoneCollider == null)
            {
                continue;
            }

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = zoneCollider.transform.localToWorldMatrix;

            if (zoneCollider is BoxCollider box)
            {
                if (drawFill)
                {
                    Gizmos.color = fillColor;
                    Gizmos.DrawCube(box.center, box.size);
                }

                Gizmos.color = outlineColor;
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (zoneCollider is SphereCollider sphere)
            {
                if (drawFill)
                {
                    Gizmos.color = fillColor;
                    Gizmos.DrawSphere(sphere.center, sphere.radius);
                }

                Gizmos.color = outlineColor;
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
            else if (zoneCollider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
            {
                if (drawFill)
                {
                    Gizmos.color = fillColor;
                    Gizmos.DrawMesh(meshCollider.sharedMesh);
                }

                Gizmos.color = outlineColor;
                Gizmos.DrawWireMesh(meshCollider.sharedMesh);
            }
            else
            {
                // Unknown collider types still get a useful fallback visualization.
                Gizmos.matrix = Matrix4x4.identity;
                if (drawFill)
                {
                    Gizmos.color = fillColor;
                    Gizmos.DrawCube(zoneCollider.bounds.center, zoneCollider.bounds.size);
                }

                Gizmos.color = outlineColor;
                Gizmos.DrawWireCube(zoneCollider.bounds.center, zoneCollider.bounds.size);
            }

            Gizmos.matrix = previousMatrix;
        }
    }
}
