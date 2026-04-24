using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace CIS5680VRGame.Balls
{
    public enum BallType
    {
        Teleport = 0,
        Sonar = 1,
        StickyPulse = 2,
    }

    public readonly struct BallImpactContext
    {
        public readonly Vector3 HitPoint;
        public readonly Vector3 HitNormal;
        public readonly GameObject BallObject;
        public readonly Collision Collision;
        public readonly Collider HitCollider;

        public BallImpactContext(Vector3 hitPoint, Vector3 hitNormal, GameObject ballObject, Collision collision)
        {
            HitPoint = hitPoint;
            HitNormal = hitNormal;
            BallObject = ballObject;
            Collision = collision;
            HitCollider = collision != null ? collision.collider : null;
        }

        public BallImpactContext(Vector3 hitPoint, Vector3 hitNormal, GameObject ballObject, Collider hitCollider)
        {
            HitPoint = hitPoint;
            HitNormal = hitNormal;
            BallObject = ballObject;
            Collision = null;
            HitCollider = hitCollider;
        }
    }

    public abstract class BallImpactEffect : MonoBehaviour
    {
        [SerializeField] BallType m_BallType = BallType.Teleport;

        public BallType BallType => m_BallType;

        public abstract void Apply(in BallImpactContext context);

        public virtual bool ShouldDestroyBallAfterImpact(in BallImpactContext context)
        {
            return true;
        }
    }

    public sealed class BallFadeOut : MonoBehaviour
    {
        const float DefaultLingerSeconds = 0.2f;
        const float DefaultFadeSeconds = 0.55f;
        const float EndScaleMultiplier = 0.82f;

        Renderer[] m_Renderers;
        Material[][] m_MaterialsByRenderer;
        Color[][] m_BaseColorsByRenderer;
        Color[][] m_BaseEmissionColorsByRenderer;
        Vector3 m_StartScale;
        bool m_IsFading;

        public static bool Begin(GameObject target, float lingerSeconds = DefaultLingerSeconds, float fadeSeconds = DefaultFadeSeconds)
        {
            if (target == null)
                return false;

            BallFadeOut fadeOut = target.GetComponent<BallFadeOut>();
            if (fadeOut == null)
                fadeOut = target.AddComponent<BallFadeOut>();

            fadeOut.BeginFade(lingerSeconds, fadeSeconds);
            return true;
        }

        void BeginFade(float lingerSeconds, float fadeSeconds)
        {
            if (m_IsFading)
                return;

            m_IsFading = true;
            m_StartScale = transform.localScale;
            StopPhysicsAndInteraction();
            PrepareRuntimeMaterials();
            StartCoroutine(FadeRoutine(Mathf.Max(0f, lingerSeconds), Mathf.Max(0.01f, fadeSeconds)));
        }

        void StopPhysicsAndInteraction()
        {
            Rigidbody rigidbody = GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                if (!rigidbody.isKinematic)
                {
                    rigidbody.velocity = Vector3.zero;
                    rigidbody.angularVelocity = Vector3.zero;
                }

                rigidbody.useGravity = false;
                rigidbody.detectCollisions = false;
                rigidbody.isKinematic = true;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = false;
            }

            XRGrabInteractable grabInteractable = GetComponent<XRGrabInteractable>();
            if (grabInteractable != null)
                grabInteractable.enabled = false;

            BallHoverInfoDisplay hoverInfoDisplay = GetComponent<BallHoverInfoDisplay>();
            if (hoverInfoDisplay != null)
                hoverInfoDisplay.enabled = false;
        }

        void PrepareRuntimeMaterials()
        {
            m_Renderers = GetComponentsInChildren<Renderer>(true);
            m_MaterialsByRenderer = new Material[m_Renderers.Length][];
            m_BaseColorsByRenderer = new Color[m_Renderers.Length][];
            m_BaseEmissionColorsByRenderer = new Color[m_Renderers.Length][];

            for (int i = 0; i < m_Renderers.Length; i++)
            {
                Renderer targetRenderer = m_Renderers[i];
                if (targetRenderer == null)
                    continue;

                Material[] materials = targetRenderer.materials;
                m_MaterialsByRenderer[i] = materials;
                m_BaseColorsByRenderer[i] = new Color[materials.Length];
                m_BaseEmissionColorsByRenderer[i] = new Color[materials.Length];

                for (int j = 0; j < materials.Length; j++)
                {
                    Material material = materials[j];
                    if (material == null)
                        continue;

                    m_BaseColorsByRenderer[i][j] = ResolveMaterialColor(material);
                    m_BaseEmissionColorsByRenderer[i][j] = material.HasProperty("_EmissionColor")
                        ? material.GetColor("_EmissionColor")
                        : Color.clear;
                    ConfigureTransparentMaterial(material);
                }
            }
        }

        IEnumerator FadeRoutine(float lingerSeconds, float fadeSeconds)
        {
            if (lingerSeconds > 0f)
                yield return new WaitForSeconds(lingerSeconds);

            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeSeconds);
                float alpha = 1f - t;
                SetVisualAlpha(alpha);
                transform.localScale = Vector3.Lerp(m_StartScale, m_StartScale * EndScaleMultiplier, t);
                yield return null;
            }

            SetVisualAlpha(0f);
            Destroy(gameObject);
        }

        void SetVisualAlpha(float alpha)
        {
            if (m_MaterialsByRenderer == null)
                return;

            for (int i = 0; i < m_MaterialsByRenderer.Length; i++)
            {
                Material[] materials = m_MaterialsByRenderer[i];
                if (materials == null)
                    continue;

                for (int j = 0; j < materials.Length; j++)
                {
                    Material material = materials[j];
                    if (material == null)
                        continue;

                    Color baseColor = m_BaseColorsByRenderer[i][j];
                    baseColor.a *= alpha;
                    SetMaterialColor(material, baseColor);

                    if (material.HasProperty("_EmissionColor"))
                        material.SetColor("_EmissionColor", m_BaseEmissionColorsByRenderer[i][j] * alpha);
                }
            }
        }

        static Color ResolveMaterialColor(Material material)
        {
            if (material.HasProperty("_BaseColor"))
                return material.GetColor("_BaseColor");

            if (material.HasProperty("_Color"))
                return material.GetColor("_Color");

            return Color.white;
        }

        static void SetMaterialColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
        }

        static void ConfigureTransparentMaterial(Material material)
        {
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);

            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);

            if (material.HasProperty("_AlphaClip"))
                material.SetFloat("_AlphaClip", 0f);

            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);

            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);

            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
