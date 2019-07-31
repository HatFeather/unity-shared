using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HatFeather.Shared.CharacterControl
{
    public class FirstPersonCameraController : MonoBehaviour
    {
        [SerializeField] FirstPersonMotionController m_MotionController = null;
        [SerializeField] FirstPersonCameraInput m_Input = null;

        [Header("Look")]
        [SerializeField] bool m_InvertX = false;
        [SerializeField] bool m_InvertY = false;
        [SerializeField] float m_LookSensitivityX = 4.0f;
        [SerializeField] float m_LookSensitivityY = 4.0f;
        [SerializeField] bool m_useAcceleration = false;
        [Space]
        [SerializeField] float m_LookSmoothing = 30;
        [SerializeField] float m_MaximumX = 90;
        [SerializeField] float m_MinimumX = -90;

        [Header("Origin")]
        [SerializeField] float m_OriginSmoothing = 25;
        [SerializeField] float m_RoofOffset = .25f;
        [Space]
        [SerializeField] HeadBob m_HeadBob = new HeadBob();

        Coroutine m_KnockRoutine;
        Coroutine m_ShakeRoutine;
        Coroutine m_RumbleRoutine;
        Vector3 m_LocalOrigin;
        Vector2 m_Knock;
        Vector2 m_Shake;
        Vector2 m_Rumble;
        Vector2 m_CurrentRot;
        Vector2 m_PrevLookInput;
        float m_RumbleMagnitude01;

        public bool screenIsRumbling { get { return m_RumbleRoutine != null; } }

        public delegate void Callback();
        public event Callback tookStep;

        void Awake()
        {
            m_KnockRoutine = null;
            m_ShakeRoutine = null;
            m_RumbleRoutine = null;
            m_LocalOrigin = transform.localPosition;
            m_Knock = new Vector2();
            m_Shake = new Vector2();
            m_Rumble = new Vector2();
            m_CurrentRot = new Vector2(transform.localEulerAngles.x, transform.parent.localEulerAngles.y);
            m_PrevLookInput = new Vector2();
            m_RumbleMagnitude01 = 0f;

            transform.localRotation = Quaternion.identity;
            m_HeadBob.initialize(m_MotionController);
        }

        void OnEnable()
        {
            m_HeadBob.footStepped += onHeadBobFootStepped;
            m_MotionController.setNewParent += onMotionControllerSetNewParent;
        }

        void OnDisable()
        {
            m_HeadBob.footStepped -= onHeadBobFootStepped;
            m_MotionController.setNewParent -= onMotionControllerSetNewParent;
        }

        void Update()
        {
            m_HeadBob.step();
            lookRotation();
            positionUpdate();
        }

        void onHeadBobFootStepped()
        {
            if (tookStep != null)
                tookStep();
        }

        void onMotionControllerSetNewParent()
        {
            // Must update look Y rotation to match local rotation of new parent
            // or you'll get ugly glitches in rotation
            m_CurrentRot.y = transform.parent.localEulerAngles.y;
        }

        void positionUpdate()
        {
            m_LocalOrigin = Vector3.up * (m_MotionController.characterController.height - m_RoofOffset);
            transform.localPosition = m_LocalOrigin;

            Vector3 targetLocalPosition = m_LocalOrigin;
            if (m_HeadBob.useMotionBob)
                targetLocalPosition += m_MotionController.transform.right * m_HeadBob.motionBobOffset.x + m_MotionController.transform.up * m_HeadBob.motionBobOffset.y;
            if (m_HeadBob.useLandBob)
                targetLocalPosition -= m_MotionController.transform.up * m_HeadBob.landBobOffset;
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPosition, m_OriginSmoothing * Time.fixedDeltaTime);
        }

        void lookRotation()
        {
            Vector2 lookInput = Vector2.Lerp(m_PrevLookInput, getLookInput(), m_LookSmoothing * Time.deltaTime);
            Vector2 rotDelta = lookInput + m_Shake + m_Knock + m_Rumble;
            m_CurrentRot.x = Mathf.Clamp(m_CurrentRot.x - rotDelta.x, m_MinimumX, m_MaximumX);
            m_CurrentRot.y = (m_CurrentRot.y + rotDelta.y) % 360f;

            transform.localRotation = Quaternion.Euler(m_CurrentRot.x, 0f, 0f);
            transform.parent.localRotation = Quaternion.Euler(0f, m_CurrentRot.y, 0f);

            m_PrevLookInput = lookInput;
        }

        Vector2 getLookInput()
        {
            Vector2 lookInput = new Vector2();
            lookInput.x = m_Input.mouseY.Axis * m_LookSensitivityY;
            lookInput.y = m_Input.mouseX.Axis * m_LookSensitivityX;
            lookInput.x *= m_InvertY ? -1 : 1;
            lookInput.y *= m_InvertX ? -1 : 1;

            if (m_useAcceleration)
            {
                const float MAGNITUDE_MULITPLIER = 11f;
                const float ACCELERATION_MULTIPLIER = .18f;

                lookInput.x = ACCELERATION_MULTIPLIER * lookInput.x * Mathf.Abs(lookInput.x);
                lookInput.y = ACCELERATION_MULTIPLIER * lookInput.y * Mathf.Abs(lookInput.y);

                float lookXMagnitude = m_LookSensitivityY * MAGNITUDE_MULITPLIER;
                float lookYMagnitude = m_LookSensitivityX * MAGNITUDE_MULITPLIER;
                lookInput.x = Mathf.Clamp(lookInput.x, -lookXMagnitude, lookXMagnitude);
                lookInput.y = Mathf.Clamp(lookInput.y, -lookYMagnitude, lookYMagnitude);
            }

            return lookInput;
        }

        public void shakeScreen(float magnitude01, float duration)
        {
            if (m_ShakeRoutine != null)
                StopCoroutine(m_ShakeRoutine);
            m_ShakeRoutine = StartCoroutine(shakeScreenRoutine(magnitude01, duration));
        }

        public void knockScreen(float magnitude01, Vector2 direction)
        {
            if (m_KnockRoutine != null)
                StopCoroutine(m_KnockRoutine);
            m_KnockRoutine = StartCoroutine(knockScreenRoutine(magnitude01, direction));
        }

        public void startRumble(float magnitude01)
        {
            setRumbleMagnitude(magnitude01);
            startRumble();
        }

        public void startRumble()
        {
            if (m_RumbleRoutine == null)
                m_RumbleRoutine = StartCoroutine(rumbleScreenRoutine());
        }

        public void setRumbleMagnitude(float magnitude01)
        {
            m_RumbleMagnitude01 = Mathf.Clamp01(magnitude01);
        }

        public void stopRumble()
        {
            if (m_RumbleRoutine != null)
                StopCoroutine(m_RumbleRoutine);
            m_RumbleRoutine = null;
            m_Rumble = Vector2.zero;
            m_RumbleMagnitude01 = 0f;
        }

        IEnumerator shakeScreenRoutine(float magnitude01, float duration)
        {
            const float MAX_MAGNITUDE = 5f;
            const float MIN_RATE = 20f;
            const float MAX_RATE = 33f;

            float magnitude = Mathf.Clamp01(magnitude01) * MAX_MAGNITUDE;
            float xRate = Random.Range(MIN_RATE, MAX_RATE);
            float yRate = Random.Range(MIN_RATE, MAX_RATE);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                m_Shake.x = Mathf.Lerp(Mathf.Cos(t * xRate * duration) * magnitude, 0, t);
                m_Shake.y = Mathf.Lerp(Mathf.Sin(t * yRate * duration) * magnitude, 0, t);
                yield return new WaitForEndOfFrame();
            }

            m_Shake = Vector2.zero;
            m_ShakeRoutine = null;
        }

        IEnumerator knockScreenRoutine(float magnitude01, Vector2 direction)
        {
            const float MAX_MAGNITUDE = 4f;
            const float MAX_DURATION = .275f;

            magnitude01 = Mathf.Clamp01(magnitude01);
            m_Knock = direction.normalized * magnitude01 * MAX_MAGNITUDE;
            float duration = magnitude01 * MAX_DURATION;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                m_Knock = Vector2.Lerp(m_Knock, Vector2.zero, t);
                yield return new WaitForEndOfFrame();
            }

            m_Knock = Vector2.zero;
            m_KnockRoutine = null;
        }

        IEnumerator rumbleScreenRoutine()
        {
            const float MAX_MAGNITUDE = .7f;
            const float MIN_RATE = 21f;
            const float MAX_RATE = 26f;

            float xRate = Random.Range(MIN_RATE, MAX_RATE);
            float yRate = Random.Range(MIN_RATE, MAX_RATE);

            float t = 0f;
            while (true)
            {
                t += Time.deltaTime;

                float magnitude = Mathf.Lerp(0, MAX_MAGNITUDE, m_RumbleMagnitude01);
                m_Rumble.x = Mathf.Cos(t * xRate) * magnitude;
                m_Rumble.y = Mathf.Sin(t * yRate) * magnitude;
                yield return new WaitForEndOfFrame();
            }
        }
    }
}
