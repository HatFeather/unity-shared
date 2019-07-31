using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HatFeather.Shared.CharacterControl
{
    [System.Serializable]
    public class HeadBob
    {
        [Header("Motion")]
        [SerializeField] bool m_UseMotionBob = true;
        [SerializeField] Vector2 m_Scale = new Vector2(.05f, .075f);
        [SerializeField] float m_XToYRatio = 2f;
        [SerializeField] float m_StabilizeRate = 5f;
        [SerializeField] float m_StrideInterval = 3f;
        [Range(0f, 1f), SerializeField] float m_RunStrideLengthen = 1f;
        [Range(0f, 1f), SerializeField] float m_CrouchBobScale = .5f;
        [SerializeField] float m_CrouchSpeedMult = 2f;
        [Space, Header("Landing")]
        [SerializeField] bool m_UseLandBob = true;
        [SerializeField] float m_LandBobDuration = .15f;
        [SerializeField] float m_LandBobAmount = .15f;

        FirstPersonMotionController m_MotionController = null;
        Coroutine m_LandRoutine = null;
        Coroutine m_MotionCorrectionRoutine = null;
        Vector2 m_CyclePosition = new Vector2();
        Vector2 m_MotionBobOffset = new Vector2();
        float m_LandBobOffset = 0f;
        float m_MotionCorrection = 0f;

        public bool useLandBob { get { return m_UseLandBob; } }
        public bool useMotionBob { get { return m_UseMotionBob; } }
        public Vector2 motionBobOffset { get { return m_MotionBobOffset; } }
        public float landBobOffset { get { return m_LandBobOffset; } }

        public delegate void Callback();
        public event Callback footStepped;

        public void initialize(FirstPersonMotionController motionController)
        {
            this.m_MotionController = motionController;
            motionController.landed += onLand;
        }

        public void step()
        {
            if (!m_UseMotionBob)
                return;

            updateMotionBob();
        }

        void onLand()
        {
            if (!m_UseMotionBob)
                return;

            if (m_LandRoutine != null)
                m_MotionController.StopCoroutine(m_LandRoutine);
            m_LandRoutine = m_MotionController.StartCoroutine(landCycle());
        }

        void updateMotionBob()
        {
            const float CYCLE_TIME = 2f;

            bool isBobbing = m_MotionController != null && m_MotionController.isGrounded && m_MotionController.desiresMove;
            if (isBobbing)
            {
                float speed = m_MotionController.characterController.velocity.magnitude * (m_MotionController.isRunning ? m_RunStrideLengthen : 1f);
                if (m_MotionController.isCrouched)
                    speed *= m_CrouchSpeedMult;

                if (m_MotionCorrectionRoutine == null)
                    m_MotionCorrectionRoutine = m_MotionController.StartCoroutine(motionCorrectionCycle());

                m_MotionBobOffset.x = Mathf.Lerp(m_MotionBobOffset.x, Mathf.Sin(m_CyclePosition.x * Mathf.PI) * m_Scale.x, m_MotionCorrection);
                m_MotionBobOffset.y = Mathf.Lerp(m_MotionBobOffset.y, Mathf.Sin(m_CyclePosition.y * Mathf.PI) * m_Scale.y, m_MotionCorrection);

                m_CyclePosition.x += (speed * Time.deltaTime) / m_StrideInterval;
                m_CyclePosition.y += ((speed * Time.deltaTime) / m_StrideInterval) * m_XToYRatio;

                if (m_CyclePosition.x > CYCLE_TIME)
                    m_CyclePosition.x -= CYCLE_TIME;
                if (m_CyclePosition.y > CYCLE_TIME)
                {
                    m_CyclePosition.y -= CYCLE_TIME;
                    footStepped();
                }
            }
            else
            {
                m_MotionBobOffset.x = Mathf.Lerp(m_MotionBobOffset.x, 0, Time.deltaTime * m_StabilizeRate);
                m_MotionBobOffset.y = Mathf.Lerp(m_MotionBobOffset.y, 0, Time.deltaTime * m_StabilizeRate * m_XToYRatio);

                m_CyclePosition.x = Mathf.Asin(m_MotionBobOffset.x) / Mathf.PI;
                m_CyclePosition.y = Mathf.Asin(m_MotionBobOffset.y) / Mathf.PI;

                m_MotionCorrectionRoutine = null; // Reset routine
            }
            if (m_MotionController.isCrouched)
                m_MotionBobOffset *= m_CrouchBobScale;
        }

        IEnumerator motionCorrectionCycle()
        {
            m_MotionCorrection = 0;
            // 1 second of correction
            while (m_MotionCorrection < 1)
            {
                m_MotionCorrection += Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }
            m_MotionCorrection = 1f;
        }

        IEnumerator landCycle()
        {
            float t = 0f;
            while (t < m_LandBobDuration)
            {
                m_LandBobOffset = Mathf.Lerp(m_LandBobOffset, m_LandBobAmount, t / m_LandBobDuration);
                t += Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }

            t = 0f;
            while (t < m_LandBobDuration)
            {
                m_LandBobOffset = Mathf.Lerp(m_LandBobAmount, 0f, t / m_LandBobDuration);
                t += Time.deltaTime;
                yield return new WaitForEndOfFrame();
            }

            m_LandBobOffset = 0f;
            m_LandRoutine = null;
        }
    }
}
