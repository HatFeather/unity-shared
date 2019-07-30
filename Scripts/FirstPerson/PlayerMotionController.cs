using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotionController : MonoBehaviour
{
    const string JUMP_BUTTON = "Jump";
    const string CROUCH_BUTTON = "Crouch";
    const string RUN_BUTTON = "Run";
    const string HORIZONTAL_AXIS = "Horizontal";
    const string VERTICAL_AXIS = "Vertical";
    const float MIN_RECOGNIZED_MOVE_INPUT = .001f;
    const float HEIGHT_ERROR = .0005f;
    const float GROUND_DELTA_POS_MULT = 5.5f;
    const float GROUND_DELTA_EULER_MULT = .17f;

    // [SerializeField] float m_CharacterMass = 10f;
    [SerializeField] float m_Gravity = -9.81f;

    [Header("Motion")]
    [SerializeField] Speed m_Speed = Speed.defaults;
    [SerializeField] float m_Acceleration = 10f;
    [Tooltip("x-axis: [-1 (backward dir), 1 (forward dir)]\ny-axis: magnitude of move")]
    [SerializeField] AnimationCurve m_DirEffector = AnimationCurve.EaseInOut(-1, .7f, 1, 1.1f);
    [Range(0, 1), SerializeField] float m_SlopeEffector = .2f;
    [Space]
    [SerializeField] float m_JumpSpeed = 6f;
    [Tooltip("How long before a jump input is revoked?")]
    [SerializeField] float m_JumpRevokeDelay = .2f;
    [Range(0, 1), SerializeField] float m_AirControl = .75f;
    [Tooltip("How long you must be in air before registering that you're in air?")]
    [SerializeField] float m_AirRegisterTime = .3f;
    [Space]
    [SerializeField] string[] m_SlideTags;

    [Header("Run Energy")]
    [SerializeField] int m_RqdRunEnergy = 10;
    [SerializeField] int m_MaxRunEnergy = 30;
    [SerializeField] float m_RegenRunTime = .3f;
    [SerializeField] float m_DegenRunTime = .35f;
    [Tooltip("x-axis: [0 (No Energy), 1 (Full Energy)]\ny-axis: magnitude of run")]
    [SerializeField] AnimationCurve m_RunEnergyCurve = AnimationCurve.EaseInOut(0, .5f, 1, 1f);

    [Header("Character Heights")]
    [SerializeField] float m_StandingHeight = 2f;
    [SerializeField] float m_CrouchedHeight = 1.3f;
    [SerializeField] float m_HeightCheckDist = .1f;
    [SerializeField] float m_HeightDeltaRate = 1.5f;
    [SerializeField] HeightState m_HeightState = HeightState.standing;

    [Header("Grounding")]
    [SerializeField] float m_GroundCheckDist = .2f;
    [SerializeField] float m_GroundCheckOffset = .03f;
    [SerializeField] LayerMask m_GroundMask = Physics.AllLayers;
    [SerializeField] LayerMask m_ParentableMask = 0;

    [Header("Overlap Prevention (Experimental)")]
    [SerializeField] bool m_AvoidOverlaps = false;
    [SerializeField] LayerMask m_OverlappableMask = 0;
    // [SerializeField] float m_OverlapAvoidance = .135f;

    [Space]
    [SerializeField] bool m_Debug = false;

    CharacterController m_CharacterController = null;
    GroundInfo m_GroundInfo = new GroundInfo();
    Coroutine m_HeightRoutine = null;
    Coroutine m_JumpRequest = null;
    Coroutine m_RunEnergyRoutine = null;
    EnergyState m_RunEnergyState = EnergyState.idle;
    HeightState m_PrevValidHeightState = HeightState.standing;
    Vector3 m_StartScale = new Vector3();
    Vector3 m_SimulatedPosition = new Vector3();
    Vector3 m_LastKnownGroundContact = new Vector3();
    Vector3 m_LastKnownPrevGroundPos = new Vector3();
    Vector3 m_LastKnownPrevGroundEuler = new Vector3();
    Vector3 m_LastKnownGroundDeltaPos = new Vector3();
    Vector3 m_LastKnownGroundDeltaEuler = new Vector3();
    Vector3 m_PrevSlideMove = new Vector3();
    Vector3 m_PrevSlideInputMove = new Vector3();
    bool m_IsSliding = false;
    bool m_PreviouslySliding = false;
    bool m_IsJumping = false;
    bool m_IsGrounded = false;
    bool m_PreviouslyGrounded = false;
    bool m_StartedValidRun = false;
    int m_RunEnergy = 0;
    float m_AirbornTime = 0;

    public CharacterController characterController
    { get { return m_CharacterController; } }

    public string groundTag
    { get { return m_GroundInfo.tag; } }

    public bool isStanding
    { get { return heightState == HeightState.standing; } }

    public bool isCrouched
    { get { return heightState == HeightState.crouched; } }

    public bool isGrounded
    { get { return m_IsGrounded; } }

    public bool isSliding
    { get { return m_IsGrounded; } }

    public bool isRunning
    { get { return m_RunEnergyState == EnergyState.decreasing; } }

    public bool desiresMove
    { get { return Mathf.Abs(horizontal) + Mathf.Abs(vertical) > MIN_RECOGNIZED_MOVE_INPUT; } }

    public bool isChangingHeight
    { get { return m_HeightRoutine != null; } }

    public float runEnergy01
    { get { return (float)m_RunEnergy / (float)m_MaxRunEnergy; } }

    bool shouldJump
    { get { return m_JumpRequest != null; } }

    bool pressedJump
    { get { return Input.GetButtonDown(JUMP_BUTTON); } }

    bool pressingJump
    { get { return Input.GetButton(JUMP_BUTTON); } }

    bool releasedJump
    { get { return Input.GetButtonUp(JUMP_BUTTON); } }

    bool pressedCrouch
    { get { return Input.GetButtonDown(CROUCH_BUTTON); } }

    bool pressingCrouch
    { get { return Input.GetButton(CROUCH_BUTTON); } }

    bool releasedCrouch
    { get { return Input.GetButtonUp(CROUCH_BUTTON); } }

    bool pressedRun
    { get { return Input.GetButtonDown(RUN_BUTTON); } }

    bool pressingRun
    { get { return Input.GetButton(RUN_BUTTON); } }

    bool releasedRun
    { get { return Input.GetButtonUp(RUN_BUTTON); } }

    float horizontal
    { get { return Input.GetAxisRaw(HORIZONTAL_AXIS); } }  // TODO: No raw input for controllers

    float vertical
    { get { return Input.GetAxisRaw(VERTICAL_AXIS); } }      // TODO: No raw input for controllers

    HeightState heightState
    {
        get { return m_HeightState; }
        set
        {
            HeightState prev = m_HeightState;
            m_HeightState = value;
            if (prev != value)
            {
                if (m_HeightRoutine != null)
                    StopCoroutine(m_HeightRoutine);
                m_HeightRoutine = StartCoroutine(lerpCharacterHeight(value));
            }
        }
    }

    EnergyState runEnergyState
    {
        get { return m_RunEnergyState; }
        set
        {
            EnergyState prev = m_RunEnergyState;
            m_RunEnergyState = value;
            if (prev != value)
            {
                if (m_RunEnergyRoutine != null)
                    StopCoroutine(m_RunEnergyRoutine);
                switch (value)
                {
                    case EnergyState.idle:
                        m_RunEnergyRoutine = null;
                        break;
                    case EnergyState.increasing:
                        m_RunEnergyRoutine = StartCoroutine(regenerateRunEnergy());
                        break;
                    case EnergyState.decreasing:
                        m_RunEnergyRoutine = StartCoroutine(degenerateRunEnergy());
                        break;
                }
            }
        }
    }

    public delegate void Callback();

    public event Callback landed;
    public event Callback jumped;
    public event Callback setNewParent;

    enum EnergyState
    {
        idle,
        increasing,
        decreasing,
    }

    enum HeightState
    {
        standing = 0,
        crouched = 1,
    }

    void Awake()
    {
        m_CharacterController = GetComponent<CharacterController>();
        m_RunEnergy = m_MaxRunEnergy;
        m_StartScale = transform.localScale;
        m_SimulatedPosition = transform.position;
    }

    void Start()
    {
        setCharacterHeight(getHeight(m_HeightState));
    }

    void Update()
    {
        preGroundCheckUpdate();

        groundCheck();
        slideCheck();

        postGroundCheckUpdate();
        updateMotion();

        m_SimulatedPosition = transform.position;
    }

    void OnGUI()
    {
        if (!m_Debug)
            return;

        GUI.Toggle(new Rect(16, 16, 128, 32), m_IsGrounded, " isGrounded");
        GUI.Toggle(new Rect(16, 48, 128, 32), m_IsSliding, " isSliding");
        GUI.Toggle(new Rect(16, 80, 128, 32), isCrouched, " isCrouched");
        GUI.Toggle(new Rect(16, 112, 128, 32), m_IsJumping, " isJumping");
        GUI.Toggle(new Rect(16, 144, 128, 32), isRunning, string.Format(" isRunning ({0})", m_RunEnergy));
        GUI.Box(new Rect(16, 164, runEnergy01 * 120 + 8, 8), "");
    }

    // void OnControllerColliderHit(ControllerColliderHit hit)
    // {
    //     Rigidbody body = hit.collider.attachedRigidbody;
    //     if (body == null || body.isKinematic)
    //         return;

    //     // Vector3 thisPos = transform.position;
    //     // Vector3 otherPos = hit.collider.transform.position;
    //     float dot = Vector3.Dot(hit.moveDirection, body.velocity);
    //     if (dot > 0)
    //         return;

    //     print("Here");

    //     // Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);
    //     // body.velocity = pushDir * pushPower;
    // }

    #region motion

    void updateMotion()
    {
        Vector3 motion;
        Vector2 input = new Vector2(horizontal, vertical);
        if (m_IsGrounded)
        {
            if (m_IsSliding)
                slidingMotion(input, Time.deltaTime, out motion);
            else if (shouldJump)
            {
                jumpMotion(input, Time.deltaTime, out motion);
                m_IsJumping = true;
                if (jumped != null)
                    jumped();

                StopCoroutine(m_JumpRequest);
                m_JumpRequest = null;
            }
            else
                planarMotion(input, Time.deltaTime, out motion);
        }
        else
            airMotion(input, Time.deltaTime, out motion);

        // Always activate collisions on controller
        if (motion == Vector3.zero)
            motion.y = -Time.deltaTime;

        if (m_AvoidOverlaps)
            avoidPredictedOverlaps(Time.deltaTime, ref motion);

        m_CharacterController.Move(motion);
    }

    float getSpeed(Vector2 moveInput)
    {
        float speed;
        if (isCrouched)
            speed = this.m_Speed.crouched;
        else if (isRunning)
            speed = Mathf.Lerp(this.m_Speed.walking, this.m_Speed.running, m_RunEnergyCurve.Evaluate(runEnergy01));
        else
            speed = this.m_Speed.walking;

        float dot = Vector2.Dot(moveInput, Vector2.up);
        speed *= m_DirEffector.Evaluate(dot);
        speed *= Mathf.Clamp01(moveInput.magnitude);

        return speed;
    }

    Vector3 getAdditveMotionFromGroundDelta(float deltaTime)
    {
        // https://en.wikipedia.org/wiki/Angular_velocity
        // TODO: Support all euler axes [see ref ^]
        // Currently only supports y axis

        Vector3 difference2D = m_LastKnownGroundContact - m_LastKnownPrevGroundPos;
        difference2D.y = 0;
        Vector3 dir = Vector3.Cross(difference2D.normalized, Vector3.down);
        float velocity = m_LastKnownGroundDeltaEuler.y * Mathf.Deg2Rad / deltaTime * difference2D.magnitude;

        // Angular velocity + delta ground pos
        return velocity * dir + m_LastKnownGroundDeltaPos;
    }

    // Experimental
    void avoidPredictedOverlaps(float deltaTime, ref Vector3 motion)
    {
        Vector3 point1 = m_SimulatedPosition + Vector3.up * m_CharacterController.radius;
        Vector3 point2 = m_SimulatedPosition + Vector3.up * (m_CharacterController.height - m_CharacterController.radius);
        float radius = m_CharacterController.radius;
        // Vector3 direction = (new Vector3(motion.x, 0, motion.z)).normalized;
        // float distance = motion.magnitude + m_OverlapAvoidance;
        int mask = m_OverlappableMask;
        QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        Collider[] overlaps = Physics.OverlapCapsule(point1, point2, radius, mask, triggerInteraction);
        for (int i = 0; i < overlaps.Length; ++i)
        {
            Vector3 a = m_SimulatedPosition;
            Vector3 b = overlaps[i].transform.position;
            Vector3 outDir;
            float outDist;

            Physics.ComputePenetration(m_CharacterController, a, Quaternion.identity, overlaps[i], b, Quaternion.identity, out outDir, out outDist);
            motion += outDir * outDist;
        }

        // int closestHit = -1;
        // float minDist = float.MaxValue;
        // RaycastHit[] hits = Physics.CapsuleCastAll(point1, point2, radius, direction, distance, mask, triggerInteraction);

        // for (int i = 0; i < hits.Length; ++i)
        // {
        //     if (hits[i].transform == transform)
        //         continue;

        //     float dist = hits[i].distance;
        //     if (dist < minDist)
        //     {
        //         closestHit = i;
        //         minDist = dist;
        //     }
        // }

        // if (closestHit != -1)
        // {
        //     RaycastHit hit = hits[closestHit];

        //     point1.Set(m_SimulatedPosition.x, 0, m_SimulatedPosition.z);
        //     point2.Set(hit.point.x, 0, hit.point.z);
        //     Vector3 normal = (point1 - point2).normalized;
        //     float dot = Vector3.Dot(direction, normal);
            
        //     float prevY = motion.y;
        //     motion = (normal * -dot + direction) * motion.magnitude;
        //     motion.y = prevY;
        // }
    }

    void planarMotion(Vector2 input, float deltaTime, out Vector3 motion)
    {
        // print("Planar motion");

        Vector3 moveDir = Vector3.ProjectOnPlane(transform.right * input.x + transform.forward * input.y, m_GroundInfo.normal).normalized;
        float dot = -Vector3.Dot(moveDir, m_GroundInfo.orthogonal);
        float slopeSlow = Mathf.Lerp(1, 0, dot > 0 ? 0 : Mathf.Abs(dot));
        float targetSpeed = Mathf.Lerp(getSpeed(input), 0, slopeSlow * m_GroundInfo.slope / 90 * m_SlopeEffector);

        float t = deltaTime * m_Acceleration;
        motion = Vector3.Lerp(m_CharacterController.velocity, moveDir * targetSpeed, t);
        // if (!m_IsJumping)
        // motion.y -= m_GroundStick;

        motion *= deltaTime;
    }

    void slidingMotion(Vector2 input, float deltaTime, out Vector3 motion)
    {
        // print("Sliding motion");

        Vector3 moveDir = (transform.right * input.x + transform.forward * input.y).normalized;
        float targetSpeed = getSpeed(input);

        Vector3 flatNormal = new Vector3(m_GroundInfo.normal.x, 0, m_GroundInfo.normal.z).normalized;
        float dot = Vector3.Dot(moveDir, flatNormal);
        Vector3 inputMove = (dot < 0 ? flatNormal * -dot + moveDir : moveDir) * targetSpeed;
        inputMove = Vector3.Lerp(m_PrevSlideInputMove, inputMove, deltaTime * m_Acceleration);
        m_PrevSlideInputMove = inputMove;

        // Ignoring mass because factors with other variables
        float parallelForce = -m_Gravity * Mathf.Sin(Mathf.Deg2Rad * m_GroundInfo.slope);
        Vector3 slideMove = m_GroundInfo.orthogonal * parallelForce + m_PrevSlideMove;
        slideMove = Vector3.Project(Vector3.Lerp(m_PrevSlideMove, slideMove, deltaTime), m_GroundInfo.orthogonal);
        m_PrevSlideMove = slideMove;

        motion = inputMove + slideMove;
        motion *= deltaTime;
    }

    void jumpMotion(Vector2 input, float deltaTime, out Vector3 motion)
    {
        // print("Jump motion");

        float targetSpeed = Mathf.Lerp(getSpeed(input), 0, m_GroundInfo.slope / 90 * m_SlopeEffector);
        Vector3 moveDir = (transform.right * input.x + transform.forward * input.y).normalized;

        motion = Vector3.Lerp(m_CharacterController.velocity, Vector3.ProjectOnPlane(moveDir, m_GroundInfo.normal) *
            targetSpeed, deltaTime * m_Acceleration);
        motion.y = m_JumpSpeed;
        motion += getAdditveMotionFromGroundDelta(deltaTime);

        motion *= deltaTime;
    }

    void airMotion(Vector2 input, float deltaTime, out Vector3 motion)
    {
        // print("Air motion");

        float targetSpeed = getSpeed(input);
        Vector3 moveDir = (transform.right * input.x + transform.forward * input.y).normalized;

        float t = m_AirControl * deltaTime * m_Acceleration;
        motion = Vector3.Lerp(m_CharacterController.velocity, moveDir * targetSpeed, t);
        motion.y = m_CharacterController.velocity.y + m_Gravity * deltaTime;

        Vector3 groundMotion = getAdditveMotionFromGroundDelta(deltaTime);
        motion.x += groundMotion.x;
        motion.z += groundMotion.z;

        motion *= deltaTime;
    }

    IEnumerator regenerateRunEnergy()
    {
        while (m_RunEnergy < m_MaxRunEnergy)
        {
            yield return new WaitForSeconds(m_RegenRunTime);
            ++m_RunEnergy;
        }
        m_RunEnergyRoutine = null;
        m_RunEnergyState = EnergyState.idle;
    }

    IEnumerator degenerateRunEnergy()
    {
        while (m_RunEnergy > 0)
        {
            yield return new WaitForSeconds(m_DegenRunTime);
            --m_RunEnergy;
        }
        m_RunEnergyRoutine = null;
        m_RunEnergyState = EnergyState.idle;
    }

    IEnumerator revokeJumpRequestAfterDelay()
    {
        yield return new WaitForSeconds(m_JumpRevokeDelay);
        m_JumpRequest = null;
    }

    #endregion

    #region checks

    void preGroundCheckUpdate()
    {
        // Height Adjusting
        if (pressingCrouch && m_IsGrounded)
            heightState = HeightState.crouched;
        else
            heightState = HeightState.standing;
        if (isChangingHeight && isHeightObstructed(m_HeightState))
            heightState = m_PrevValidHeightState;

        // Running
        if (pressedRun && m_RunEnergy > m_RqdRunEnergy)
            m_StartedValidRun = true;
        if (desiresMove && !isCrouched && pressingRun && m_RunEnergy > 0 && m_StartedValidRun)
            runEnergyState = EnergyState.decreasing;
        else
        {
            if (!pressingRun || m_RunEnergy == 0)
                m_StartedValidRun = false;
            runEnergyState = EnergyState.increasing;
        }

        // Jumping
        if (pressedJump)
        {
            if (m_JumpRequest != null)
                StopCoroutine(m_JumpRequest);
            m_JumpRequest = StartCoroutine(revokeJumpRequestAfterDelay());
        }

        // Done before groundCheck() to ensure value is not reset when used
        m_AirbornTime = m_IsGrounded ? 0f : m_AirbornTime + Time.deltaTime;
    }

    void postGroundCheckUpdate()
    {
        // Update based on new grounded state
        Transform prevParent = transform.parent;
        if (m_IsGrounded)
        {
            if (!m_PreviouslyGrounded || m_IsSliding)
                m_IsJumping = false;
            if (!m_PreviouslyGrounded && !m_IsSliding && m_AirbornTime > m_AirRegisterTime && landed != null)
                landed();

            if (m_ParentableMask == (m_ParentableMask | (1 << m_GroundInfo.mask)))
                transform.SetParent(m_GroundInfo.transform);

            // Ground delta update
            Vector3 currentGroundPos = m_GroundInfo.transform.position;
            Vector3 currentGroundEuler = m_GroundInfo.transform.localEulerAngles;
            if (transform.parent != prevParent || !m_PreviouslyGrounded)
            {
                // Reset for no unexpected ground delta
                m_LastKnownPrevGroundPos = currentGroundPos;
                m_LastKnownPrevGroundEuler = currentGroundEuler;
            }
            m_LastKnownGroundContact = m_GroundInfo.contact;
            m_LastKnownGroundDeltaPos = (currentGroundPos - m_LastKnownPrevGroundPos) * GROUND_DELTA_POS_MULT;
            m_LastKnownGroundDeltaEuler = (currentGroundEuler - m_LastKnownPrevGroundEuler) * GROUND_DELTA_EULER_MULT;
            m_LastKnownPrevGroundPos = currentGroundPos;
            m_LastKnownPrevGroundEuler = currentGroundEuler;
        }
        else
        {
            transform.SetParent(null);
            // Prevent previous parent from affecting scale (weird glitch)
            transform.localScale = m_StartScale;
        }
        if (prevParent != transform.parent && setNewParent != null)
            setNewParent();
    }

    void slideCheck()
    {
        bool slipperyGround = false;
        for (int i = 0; i < m_SlideTags.Length; ++i)
            if (m_GroundInfo.tag == m_SlideTags[i])
                slipperyGround = true;

        m_PreviouslySliding = m_IsSliding;
        m_IsSliding = m_IsGrounded && (m_GroundInfo.slope > m_CharacterController.slopeLimit || slipperyGround);

        if (!m_PreviouslySliding && m_IsSliding)
        {
            m_PrevSlideMove = Vector3.Project(m_CharacterController.velocity, m_GroundInfo.orthogonal);

            m_PrevSlideInputMove = Vector3.ProjectOnPlane(m_CharacterController.velocity, Vector3.up);
            float magnitude = m_PrevSlideInputMove.magnitude;
            m_PrevSlideInputMove.Normalize();
            Vector3 flatNormal = new Vector3(m_GroundInfo.normal.x, 0, m_GroundInfo.normal.z).normalized;
            float dot = Vector3.Dot(m_PrevSlideInputMove, flatNormal);
            m_PrevSlideInputMove = (dot < 0 ? flatNormal * -dot + m_PrevSlideInputMove : m_PrevSlideInputMove) * magnitude;
        }
    }

    void groundCheck()
    {
        m_PreviouslyGrounded = m_IsGrounded;

        Vector3 origin = m_SimulatedPosition + Vector3.up * m_CharacterController.radius;
        float radius = m_CharacterController.radius - m_GroundCheckOffset;
        Vector3 direction = Vector3.down;
        float distance = this.m_GroundCheckDist;
        int mask = this.m_GroundMask;
        QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        RaycastHit[] hits = Physics.SphereCastAll(origin, radius, direction, distance, mask, triggerInteraction);
        bool foundGround = false;
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform == transform)
                continue;

            m_GroundInfo.transform = hit.transform;
            m_GroundInfo.normal = hit.normal;
            m_GroundInfo.contact = hit.point;

            Vector3 groundNormalRef = m_GroundInfo.normal;
            m_GroundInfo.orthogonal.Set(groundNormalRef.x, -groundNormalRef.y, groundNormalRef.z);
            Vector3.OrthoNormalize(ref groundNormalRef, ref m_GroundInfo.orthogonal);

            m_GroundInfo.slope = Mathf.Abs(Vector3.Angle(Vector3.up, hit.normal));
            m_GroundInfo.tag = hit.collider.tag;

            m_IsGrounded = true;
            foundGround = true;
            break;
        }

        if (!foundGround)
        {
            m_IsGrounded = false;
            m_GroundInfo.clearInfo();
        }
    }

    #endregion

    #region height

    bool isHeightObstructed(HeightState desiredState)
    {
        const float WIDTH_OFFSET = .001f;
        float height = getHeight(desiredState);
        if (height < m_CharacterController.height + HEIGHT_ERROR)
            return false;

        Ray ray = new Ray(m_SimulatedPosition + Vector3.up * m_CharacterController.radius, Vector3.up);
        float distance = height - m_CharacterController.radius * 2 + m_HeightCheckDist + WIDTH_OFFSET;
        float radius = m_CharacterController.radius - WIDTH_OFFSET;
        return Physics.SphereCast(ray, radius, distance, Physics.AllLayers, QueryTriggerInteraction.Ignore);
    }

    void setCharacterHeight(float height)
    {
        m_CharacterController.height = height;
        m_CharacterController.center = Vector3.up * height / 2;
    }

    float getHeight(HeightState state)
    {
        float height;
        switch (state)
        {
            case HeightState.standing:
                height = m_StandingHeight;
                break;
            case HeightState.crouched:
                height = m_CrouchedHeight;
                break;
            default:
                throw new System.NotImplementedException(state.ToString());
        }
        return height;
    }

    IEnumerator lerpCharacterHeight(HeightState state)
    {
        float targetHeight = getHeight(heightState);
        // If target height is lower, it is guaranteed to be a valid height
        if (targetHeight < m_CharacterController.height + HEIGHT_ERROR)
            m_PrevValidHeightState = state;

        float duration = Mathf.Abs(targetHeight - m_CharacterController.height) / m_HeightDeltaRate;
        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime / duration;
            setCharacterHeight(Mathf.Lerp(m_CharacterController.height, targetHeight, t));
            yield return new WaitForEndOfFrame();
        }

        setCharacterHeight(targetHeight);
        m_HeightRoutine = null;
        m_PrevValidHeightState = state;
    }

    #endregion

    #region classes and structs

    [System.Serializable]
    public struct Speed
    {
        public float running;
        public float walking;
        public float crouched;

        public static Speed defaults
        {
            get
            {
                Speed ret = new Speed();
                ret.running = 6.5f;
                ret.walking = 4f;
                ret.crouched = 2f;
                return ret;
            }
        }
    }

    struct GroundInfo
    {
        public Transform transform;
        public Vector3 normal;
        public Vector3 contact;
        public Vector3 orthogonal;
        public float slope;
        public string tag;

        public bool containsInfo
        { get { return normal != Vector3.zero; } }

        public LayerMask mask
        { get { return transform.gameObject.layer; } }

        public void clearInfo()
        {
            transform = null;
            normal.Set(0, 0, 0);
            contact.Set(0, 0, 0);
            orthogonal.Set(0, 0, 0);
            slope = 0;
            tag = string.Empty;
        }
    }

    #endregion
}
