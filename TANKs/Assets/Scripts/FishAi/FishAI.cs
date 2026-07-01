using System.Linq;

using UnityEngine;

using System.Collections.Generic;



[RequireComponent(typeof(Rigidbody))]

public class FishAI : MonoBehaviour

{

    [Header("🧬 ข้อมูลสายพันธุ์")]

    public FishData data;

    public FishGender gender;



    [Header("📊 สถานะปัจจุบัน (Current Stats)")]

    public FishState currentState = FishState.Swimming;

    public float currentHealth = 100f;

    public float currentHunger = 100f;   // 0 = หิวตาย, 100 = อิ่ม

    public float currentMoisture = 100f; // ความชื้น (ลดเมื่ออยู่นอกน้ำ)

    public float currentMood = 100f;     // อารมณ์ (กระทบจากน้ำ/ความหิว)

    public int ageDays = 0;



    [Tooltip("ขนาดตัวปัจจุบันของปลา (หน่วย: เซนติเมตร)")]

    public float currentSize;



    [Header("⚙️ ตั้งค่าระบบนอกน้ำ")]

    public float moistureLossRate = 2.0f;



    [Header("🌊 ระบบรับรู้น้ำ (Water Detection)")]

    public float stateChangeDelay = 0.3f;

    public float baseWaterBuffer = 0.01f;

    public float sizeDepthMultiplier = 0.5f;



    [Header("📍 ระบบจุดยุทธศาสตร์ (Schooling & Roaming)")]

    public float strategicChangeInterval = 20f;

    public float roamRadius = 0.4f;



    [HideInInspector] public Vector3 strategicWaypoint; // จุดศูนย์กลางฝูง

    private float strategicTimer = 0f;



    [Header("🐟 ระบบฝูง (Schooling System)")]

    public FishAI myLeader; // จ่าฝูงที่ตามอยู่ (ถ้าเป็นตัวเองแปลว่าเป็นจ่าฝูง)

    private float uniqueOffset; // ค่าสุ่มเพื่อไม่ให้อนิเมชั่นว่ายน้ำพร้อมกันเกินไป



    private float stateChangeTimer = 0f;

    private Vector3 targetWaypoint;

    private FishAI targetPrey;



    [Header("🐟 ระบบว่ายน้ำพุ่งสลับหยุด (Dart & Glide)")]

    private bool isDarting = false;

    private float swimPhaseTimer = 0f;

    private float currentForwardSpeed = 0f;



    private Rigidbody rb;

    private Renderer cachedSandRenderer;



    private TankAIManager myManager;

    private Vector3 customVelocity;

    private bool isGrounded = false;



    [Header("⚙️ การป้องกัน")]

    public Collider tankCollider;

    private bool isObserving = false;

    private float observeTimer = 0f;



    [Header("⚙️ การเคลื่อนที่ (Movement)")]

    public float rotationSpeed = 2.0f;

    public float schoolingSpread = 0.5f;



    void Start()

    {

        rb = GetComponent<Rigidbody>();

        if (rb != null)

        {

            rb.isKinematic = true;

            rb.useGravity = false;

        }



        if (tankCollider == null)

        {

            GameObject tankObj = GameObject.FindWithTag("Tank");

            if (tankObj != null) tankCollider = tankObj.GetComponent<Collider>();



            if (tankCollider == null)

                Debug.LogWarning($"<color=yellow>⚠️ ปลา {gameObject.name} หาตู้ไม่เจอ! โปรดใส่ Tag'Tank' ให้ตู้ปลา</color>");

        }



        currentSize = data.adultSize * 0.2f;

        gender = (Random.value > 0.5f) ? FishGender.Male : FishGender.Female;

        uniqueOffset = Random.Range(0f, 1000f); // สุ่มเพื่อให้แต่ละตัวขยับไม่เหมือนกัน

        UpdateModelScale();



        myManager = GetComponentInParent<TankAIManager>();



        if (myManager == null)

        {

            myManager = FindObjectsByType<TankAIManager>(FindObjectsSortMode.None)

              .OrderBy(m => Vector3.Distance(transform.position, m.transform.position))

              .FirstOrDefault();

        }



        if (myManager != null)

        {

            myManager.RegisterFish(this);

            cachedSandRenderer = myManager.sandSim?.GetComponent<Renderer>();

        }



        strategicTimer = Random.Range(0f, strategicChangeInterval);

        FindSchool();

        UpdateStrategicPoint();

        GetNewWaypoint();

    }



    void OnDestroy()

    {

        if (myManager != null) myManager.UnregisterFish(this);

        if (TimeManager.Instance != null) TimeManager.Instance.OnHourChanged -= OnGameHourPassed;

    }



    private void OnGameHourPassed(int day, int hour)

    {

        if (currentState == FishState.Dead) return;



        float hungerDrop = (currentState == FishState.Sleeping) ? 1f : 3f;

        currentHunger = Mathf.Clamp(currentHunger - hungerDrop, 0f, 100f);



        if (currentHunger < 30f) currentMood -= 5f;



        if (myManager != null && myManager.waterQuality != null)

        {

            float phDiff = Mathf.Abs(myManager.waterQuality.ph - data.preferredPH);

            if (phDiff > 1.0f) currentMood -= phDiff * 2f;

            else currentMood += 2f;

        }



        currentMood = Mathf.Clamp(currentMood, 0f, 100f);



        if (hour == 0)

        {

            ageDays++;

            if (ageDays <= data.daysToMature)

            {

                currentSize = Mathf.Lerp(data.adultSize * 0.2f, data.adultSize, (float)ageDays / data.daysToMature);

                UpdateModelScale();

            }

        }



        if (currentHunger <= 0f) Die();

    }



    void Update()

    {

        if (currentState == FishState.Dead)

        {

            if (!isGrounded) SimulateAirbornePhysics();

            return;

        }



        CheckTankBoundaries();

        ProcessStateMachine();

    }



    private void CheckTankBoundaries()

    {

        // ... (โค้ดเดิม ไม่มีการเปลี่ยนแปลง) ...

        if (myManager == null || myManager.waterSim == null || cachedSandRenderer == null) return;



        float localWaterY = myManager.waterSim.GetHeightAtWorldPos(transform.position);

        float localSandY = myManager.sandSim.GetHeightAtWorldPos(transform.position);

        float worldWaterY = myManager.waterSim.transform.TransformPoint(new Vector3(0, localWaterY, 0)).y;

        float worldSandY = myManager.sandSim.transform.TransformPoint(new Vector3(0, localSandY, 0)).y;



        Bounds b = cachedSandRenderer.bounds;

        bool isOutsideWalls = transform.position.x < b.min.x || transform.position.x > b.max.x ||

                   transform.position.z < b.min.z || transform.position.z > b.max.z;



        bool isCompletelyOutOfWater = transform.position.y > worldWaterY + 0.05f;

        bool isTankDry = (worldWaterY - worldSandY) < 0.01f;



        FishState targetState = (isOutsideWalls || isCompletelyOutOfWater || isTankDry) ? FishState.Flopping : FishState.Swimming;



        if (currentState != targetState)

        {

            stateChangeTimer += Time.deltaTime;

            if (stateChangeTimer >= stateChangeDelay)

            {

                currentState = targetState;

                stateChangeTimer = 0f;



                if (currentState == FishState.Swimming)

                {

                    isGrounded = true;

                    customVelocity = Vector3.zero;

                    transform.localEulerAngles = new Vector3(0f, transform.localEulerAngles.y, 0f);

                    GetNewWaypoint();

                }

                else { isGrounded = false; }

            }

        }

        else { stateChangeTimer = 0f; }



        if (currentState == FishState.Flopping)

        {

            currentMoisture -= moistureLossRate * Time.deltaTime;

            if (currentMoisture <= 0) Die();

        }

        else { currentMoisture = 100f; }

    }



    private void ProcessStateMachine()

    {

        if (currentState == FishState.Swimming || currentState == FishState.Foraging)

        {

            strategicTimer -= Time.deltaTime;

            if (strategicTimer <= 0f)

            {

                UpdateStrategicPoint();

                strategicTimer = strategicChangeInterval + Random.Range(-5f, 5f);

            }

        }



        switch (currentState)

        {

            case FishState.Swimming:

                HandleSwimming();

                if (currentHunger < 50f) currentState = FishState.Foraging;

                break;

            case FishState.Foraging:

                HandleForaging();

                break;

            case FishState.Attacking:

                HandleAttacking();

                break;

            case FishState.Flopping:

                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0f, transform.eulerAngles.y, 90f), Time.deltaTime * 5f);

                if (isGrounded)

                {

                    float fishRadiusOffset = Mathf.Max(0.01f, (currentSize / 100f) * 0.25f);

                    if (!Physics.Raycast(transform.position, Vector3.down, fishRadiusOffset + 0.15f))

                    {

                        isGrounded = false;

                        customVelocity = Vector3.zero;

                    }

                }

                if (!isGrounded) SimulateAirbornePhysics();

                break;

        }

    }



    // 🌟 ค้นหาและเข้ากลุ่มฝูง (Schooling Management)

    private void FindSchool()

    {

        if (myManager == null) return;



        myLeader = this; // ค่าเริ่มต้นคือตั้งตัวเองเป็นจ่าฝูง

        List<FishAI> possibleLeaders = new List<FishAI>();



        // หาจ่าฝูงสายพันธุ์เดียวกันทั้งหมด

        foreach (FishAI fish in myManager.allFishInTank)

        {

            if (fish != this && fish.data != null && fish.data.speciesName == this.data.speciesName && fish.currentState != FishState.Dead)

            {

                if (fish.myLeader == fish) // ถ้าตัวนี้คือจ่าฝูง

                {

                    possibleLeaders.Add(fish);

                }

            }

        }



        // ลองเข้าร่วมฝูงที่ยังไม่เต็ม

        foreach (FishAI leader in possibleLeaders)

        {

            int currentFollowers = 0;

            foreach (FishAI f in myManager.allFishInTank)

            {

                if (f.myLeader == leader) currentFollowers++;

            }



            if (currentFollowers < data.preferredSchoolSize)

            {

                myLeader = leader;

                break; // ได้ฝูงแล้ว

            }

        }

    }



    // 🌟 สุ่มระดับที่ 1: หาจุดยุทธศาสตร์หรือจุดรวมฝูงปลา

    // 🌟 สุ่มระดับที่ 1: หาจุดยุทธศาสตร์หรือจุดรวมฝูงปลา

    private void UpdateStrategicPoint()

    {

        if (myManager == null || tankCollider == null) return;



        // ถ้ายกเลิกจ่าฝูงเดิม (ตายหรือสูญหาย) ให้หาใหม่

        if (myLeader == null || myLeader.currentState == FishState.Dead) FindSchool();



        // โอกาส 10% ที่ปลาลูกฝูงจะเบื่อแล้วพยายามแยกฝูงใหม่เวลาถึงรอบอัปเดต

        if (myLeader != this && Random.value < 0.10f) FindSchool();



        if (myLeader == this)

        {

            // ถ้าเป็นจ่าฝูง ให้สุ่มพิกัด "ข้ามตู้" โดยเน้นขอบตู้มากขึ้น

            Bounds b = tankCollider.bounds;

            float padding = 0.1f; // ระยะห่างจากขอบกระจกกันชน

            float edgeZone = 0.3f; // ความกว้างของพื้นที่ "โซนริมตู้"



            float rx = 0f;

            float rz = 0f;



            // เพิ่มโอกาส 70% ที่จุดยุทธศาสตร์จะอยู่ "ริมตู้" ด้านใดด้านหนึ่ง

            if (Random.value < 0.7f)

            {

                if (Random.value < 0.5f)

                {

                    // ชิดขอบซ้าย หรือ ขอบขวา

                    rx = (Random.value < 0.5f)

            ? Random.Range(b.min.x + padding, b.min.x + padding + edgeZone)

            : Random.Range(b.max.x - padding - edgeZone, b.max.x - padding);

                    // แนว Z ว่ายอิสระตั้งแต่หน้าสระถึงหลังสระ

                    rz = Random.Range(b.min.z + padding, b.max.z - padding);

                }

                else

                {

                    // ชิดขอบหน้า หรือ ขอบหลัง

                    rz = (Random.value < 0.5f)

            ? Random.Range(b.min.z + padding, b.min.z + padding + edgeZone)

            : Random.Range(b.max.z - padding - edgeZone, b.max.z - padding);

                    // แนว X ว่ายอิสระตั้งแต่ซ้ายถึงขวา

                    rx = Random.Range(b.min.x + padding, b.max.x - padding);

                }

            }

            else

            {

                // โอกาส 30% ที่เหลือ ให้จุดยุทธศาสตร์อยู่ "กลางตู้"

                rx = Random.Range(b.min.x + padding + edgeZone, b.max.x - padding - edgeZone);

                rz = Random.Range(b.min.z + padding + edgeZone, b.max.z - padding - edgeZone);

            }



            strategicWaypoint = new Vector3(rx, transform.position.y, rz);

        }

        else

        {

            // ถ้าเป็นลูกฝูง แค่ยึดจุดหมายตามจ่าฝูง

            strategicWaypoint = myLeader.strategicWaypoint;

        }

    }

    private void SimulateAirbornePhysics()

    {

        // ... (โค้ดเดิม ไม่มีการเปลี่ยนแปลง) ...

        customVelocity.y += -1.5f * Time.deltaTime;

        if (customVelocity.y < -1.5f) customVelocity.y = -1.5f;



        customVelocity.x *= Mathf.Exp(-2.5f * Time.deltaTime);

        customVelocity.z *= Mathf.Exp(-2.5f * Time.deltaTime);



        transform.position += customVelocity * Time.deltaTime;



        float fishRadiusOffset = Mathf.Max(0.01f, (currentSize / 100f) * 0.25f);

        float targetFloorY = -999f;

        bool hitValidGround = false;

        bool insideTank = false;



        if (cachedSandRenderer != null)

        {

            Bounds b = cachedSandRenderer.bounds;

            Vector3 testPos = transform.position;

            testPos.y = b.center.y;

            if (b.Contains(testPos)) insideTank = true;

        }



        if (insideTank && myManager != null && myManager.sandSim != null)

        {

            float localSandY = myManager.sandSim.GetHeightAtWorldPos(transform.position);

            Vector3 localPosSand = myManager.sandSim.transform.InverseTransformPoint(transform.position);

            localPosSand.y = localSandY;

            Vector3 worldSandPos = myManager.sandSim.transform.TransformPoint(localPosSand);

            targetFloorY = worldSandPos.y;

            hitValidGround = true;

        }

        else

        {

            float rayDistance = fishRadiusOffset + 0.1f;

            RaycastHit[] hits = Physics.RaycastAll(transform.position, Vector3.down, rayDistance);

            float closestDistance = float.MaxValue;



            foreach (RaycastHit hit in hits)

            {

                if (hit.collider.gameObject == gameObject) continue;

                if (myManager != null && (hit.collider.transform.IsChildOf(myManager.transform) || hit.collider.gameObject == myManager.gameObject)) continue;



                if (hit.distance < closestDistance)

                {

                    closestDistance = hit.distance;

                    targetFloorY = hit.point.y;

                    hitValidGround = true;

                }

            }

        }



        if (hitValidGround)

        {

            float triggerHeight = targetFloorY + fishRadiusOffset;

            if (transform.position.y <= triggerHeight)

            {

                transform.position = new Vector3(transform.position.x, triggerHeight, transform.position.z);

                if (customVelocity.y < -0.5f)

                {

                    customVelocity.y = -customVelocity.y * 0.1f;

                    customVelocity.x *= 0.5f;

                    customVelocity.z *= 0.5f;

                }

                else

                {

                    customVelocity = Vector3.zero;

                    isGrounded = true;

                }

            }

        }

    }



    private void HandleSwimming()

    {

        // ว่ายปกติ ใช้ระบบพุ่งสลับหยุด (forceContinuous = false)

        MoveTowardsTarget(data.baseSpeed, false);



        if (Vector3.Distance(transform.position, targetWaypoint) < 0.2f)

        {

            GetNewWaypoint();

        }

    }



    private void HandleForaging()

    {

        if (data.dietType == DietType.Carnivore)

        {

            if (targetPrey == null) targetPrey = myManager.FindPreyFor(this);

            if (targetPrey != null)

            {

                currentState = FishState.Attacking;

                return;

            }

        }



        // ตอนหาอาหารให้ว่ายช้าลงนิดนึง และใช้ระบบพุ่งสลับหยุด

        MoveTowardsTarget(data.baseSpeed * 0.8f, false);

        if (Vector3.Distance(transform.position, targetWaypoint) < 0.1f) GetNewWaypoint();

    }

    private void HandleAttacking()

    {

        if (targetPrey == null || targetPrey.currentState == FishState.Dead)

        {

            currentState = FishState.Swimming;

            return;

        }



        if (data.huntingStyle == HuntStyle.Ambush)

        {

            float dist = Vector3.Distance(transform.position, targetPrey.transform.position);

            // ดักซุ่ม ให้ว่ายแบบพุ่งสลับหยุดช้าๆ ถ้าเข้าใกล้ค่อยว่ายพรวด (forceContinuous = true)

            if (dist > 0.5f) MoveTowards(targetPrey.transform.position, data.baseSpeed * 0.3f, false);

            else MoveTowards(targetPrey.transform.position, data.baseSpeed * 3.0f, true);

        }

        else if (data.huntingStyle == HuntStyle.Chase)

        {

            // ไล่ล่า ให้ว่ายพรวดต่อเนื่องแบบปลาหนี/ไล่กวด

            MoveTowards(targetPrey.transform.position, data.baseSpeed * 1.5f, true);

        }



        if (Vector3.Distance(transform.position, targetPrey.transform.position) < 0.1f)

        {

            targetPrey.Die();

            currentHunger = 100f;

            currentMood += 20f;

            targetPrey = null;

            currentState = FishState.Swimming;

        }

    }

    private void MoveTowardsTarget(float speed, bool forceContinuous)

    {

        MoveTowards(targetWaypoint, speed, forceContinuous);

    }



    private void MoveTowards(Vector3 pos, float maxSpeed, bool forceContinuous)

    {

        float varianceIntensity = 0.1f;

        Vector3 noise = new Vector3(

          Mathf.Sin((Time.time + uniqueOffset) * 0.5f) * varianceIntensity,

          Mathf.Cos((Time.time + uniqueOffset) * 0.3f) * varianceIntensity,

          Mathf.Sin((Time.time + uniqueOffset) * 0.7f) * varianceIntensity

        );



        Vector3 targetDir = ((pos + noise) - transform.position).normalized;



        if (targetDir != Vector3.zero && currentState != FishState.Flopping)

        {

            float lookAheadDistance = maxSpeed * 0.7f;

            float fishRadius = Mathf.Max(0.02f, currentSize / 100f);



            // หลบสิ่งกีดขวาง

            if (isObserving)

            {

                observeTimer -= Time.deltaTime;

                maxSpeed *= 0.1f;

                if (observeTimer <= 0f) { isObserving = false; GetNewWaypoint(); return; }

            }

            else

            {

                if (Physics.SphereCast(transform.position, fishRadius, transform.forward, out RaycastHit hit, lookAheadDistance))

                {

                    bool hitWall = hit.collider.CompareTag("Tank");

                    bool hitObstacle = (myManager?.waterQuality?.tankObstacles.Contains(hit.collider) ?? false);

                    if (hitWall || hitObstacle) { isObserving = true; observeTimer = Random.Range(1.0f, 1.5f); }

                }

            }



            // 🌟 คำนวณ Hyper Level (0.0 ถึง 1.0)

            float hyperFactor = data.hyperLevel / 100f;



            if (forceContinuous)

            {

                // ว่ายพรวดต่อเนื่อง (ใช้ตอนโจมตีหรือหนี)

                currentForwardSpeed = Mathf.Lerp(currentForwardSpeed, maxSpeed, Time.deltaTime * 5f);

            }

            else

            {

                // ว่ายแบบพุ่งสลับหยุด (Dart & Glide)

                swimPhaseTimer -= Time.deltaTime;



                if (isDarting)

                {

                    // จังหวะพุ่ง: เร่งความเร็ว

                    currentForwardSpeed = Mathf.Lerp(currentForwardSpeed, maxSpeed, Time.deltaTime * 4f);



                    if (swimPhaseTimer <= 0f)

                    {

                        isDarting = false;

                        // ตั้งเวลาลอยนิ่ง: ยิ่ง Hyper น้อย ยิ่งลอยนิ่งนาน (1 - 4 วินาที)

                        swimPhaseTimer = Random.Range(1.0f, 4.0f) * (1.1f - hyperFactor);

                        if (hyperFactor > 0.8f) swimPhaseTimer *= 0.2f; // Hyper สูงแทบไม่หยุดนิ่งเลย

                    }

                }

                else

                {

                    // จังหวะลอยนิ่ง: ชะลอความเร็วจนติดลบนิดๆ (แรงพยุงน้ำดันถอยหลัง)

                    float driftSpeed = -maxSpeed * 0.05f;

                    currentForwardSpeed = Mathf.Lerp(currentForwardSpeed, driftSpeed, Time.deltaTime * 2.5f);



                    if (swimPhaseTimer <= 0f)

                    {

                        isDarting = true;

                        // ตั้งเวลาพุ่ง: ยิ่ง Hyper สูง ยิ่งพุ่งนาน (0.5 - 2.5 วินาที)

                        swimPhaseTimer = Random.Range(0.5f, 1.5f) * (0.5f + (hyperFactor * 1.5f));

                    }

                }

            }



            // 🌟 การหันหัว (Turn Speed)

            float turnSpeed = rotationSpeed;

            if (!forceContinuous)

            {

                // หันเร็วขึ้นตอนพุ่ง หันช้าๆเนียนๆตอนลอยนิ่ง

                turnSpeed = isDarting ? rotationSpeed * 1.2f : rotationSpeed * 0.5f;

                // บวกผลกระทบจาก Hyper Level ด้วย

                turnSpeed *= Mathf.Lerp(0.5f, 1.5f, hyperFactor);

            }

            if (!isObserving && Vector3.Angle(transform.forward, targetDir) > 45f) turnSpeed *= 1.5f;



            Quaternion targetRotation = Quaternion.LookRotation(targetDir);

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);



            // ขยับตัว

            float bobbing = Mathf.Sin((Time.time + uniqueOffset) * 2f) * 0.005f;

            if (currentForwardSpeed <= 0) bobbing *= 0.2f; // ลอยนิ่งจะไม่ส่ายขึ้นลงเยอะ



            Vector3 movement = transform.forward * currentForwardSpeed * Time.deltaTime;

            movement.y += bobbing * Time.deltaTime * 2f;

            Vector3 nextPos = transform.position + movement;



            // ตรวจสอบขอบตู้และสิ่งกีดขวาง (ไม่เปลี่ยนจากเดิม)

            if (myManager != null && myManager.sandSim != null)

            {

                float localSandY = myManager.sandSim.GetHeightAtWorldPos(nextPos);

                float worldSandY = myManager.sandSim.transform.TransformPoint(new Vector3(0, localSandY, 0)).y;

                if (nextPos.y < worldSandY + fishRadius) { nextPos.y = worldSandY + fishRadius; if (Random.value < 0.05f) { GetNewWaypoint(); isObserving = false; } }



                if (myManager.waterSim != null)

                {

                    float localWaterY = myManager.waterSim.GetHeightAtWorldPos(nextPos);

                    float worldWaterY = myManager.waterSim.transform.TransformPoint(new Vector3(0, localWaterY, 0)).y;

                    float maxAllowedY = worldWaterY - fishRadius;

                    if (maxAllowedY < worldSandY + fishRadius) maxAllowedY = worldWaterY;

                    if (nextPos.y > maxAllowedY) { nextPos.y = maxAllowedY; if (Random.value < 0.05f) { GetNewWaypoint(); isObserving = false; } }

                }

            }



            if (tankCollider != null)

            {

                Vector3 clampedPos = tankCollider.ClosestPoint(nextPos);

                if (Vector3.Distance(nextPos, clampedPos) > 0.002f) { nextPos = clampedPos; if (Random.value < 0.02f) { GetNewWaypoint(); isObserving = false; } }

            }

            transform.position = nextPos;

        }

    }

    // 🌟 สุ่มระดับที่ 2: สุ่มว่ายระยะใกล้ (แต่ละตัวเลือกระดับความสูงอิสระ)

    private void GetNewWaypoint()

    {

        if (tankCollider == null) return;

        if (strategicWaypoint == Vector3.zero) UpdateStrategicPoint();



        bool isLowWater = false;

        if (myManager.waterQuality != null)

        {

            float maxCap = myManager.waterQuality.GetTotalTankVolumeLiters() - myManager.waterQuality.sandVolumeLiters;

            if (myManager.waterQuality.waterVolumeLiters < maxCap * 0.5f) isLowWater = true;

        }



        Vector3 centerPos = isLowWater ? tankCollider.bounds.center : strategicWaypoint;

        float currentRoam = isLowWater ? tankCollider.bounds.extents.x : schoolingSpread;



        // สุ่ม X และ Z รอบๆ จุดยุทธศาสตร์

        float randomX = Random.Range(centerPos.x - currentRoam, centerPos.x + currentRoam);

        float randomZ = Random.Range(centerPos.z - currentRoam, centerPos.z + currentRoam);



        Bounds b = tankCollider.bounds;

        randomX = Mathf.Clamp(randomX, b.min.x + 0.05f, b.max.x - 0.05f);

        randomZ = Mathf.Clamp(randomZ, b.min.z + 0.05f, b.max.z - 0.05f);



        // 🌟 สุ่มความสูง (Y) อย่างอิสระของแต่ละตัว ไม่ตามจ่าฝูง!

        float targetY = transform.position.y;

        if (myManager != null && myManager.waterSim != null && myManager.sandSim != null)

        {

            float localWaterY = myManager.waterSim.GetHeightAtWorldPos(new Vector3(randomX, 0, randomZ));

            float localSandY = myManager.sandSim.GetHeightAtWorldPos(new Vector3(randomX, 0, randomZ));

            float wy = myManager.waterSim.transform.TransformPoint(new Vector3(0, localWaterY, 0)).y;

            float sy = myManager.sandSim.transform.TransformPoint(new Vector3(0, localSandY, 0)).y;



            float minY = sy + 0.05f;

            float maxY = wy - 0.05f;



            // สุ่ม Y แบบอิสระระหว่างพื้นและผิวน้ำก่อน

            targetY = Random.Range(minY, maxY);



            // ปรับโซนการว่ายตามสายพันธุ์

            switch (data.swimZone)

            {

                case SwimZone.Bottom: targetY = Random.Range(minY, sy + 0.2f); break;

                case SwimZone.Surface: targetY = Random.Range(wy - 0.2f, maxY); break;

                case SwimZone.Middle: targetY = Random.Range(Mathf.Lerp(minY, maxY, 0.3f), Mathf.Lerp(minY, maxY, 0.7f)); break;

            }

            targetY = Mathf.Clamp(targetY, minY, maxY);

        }



        targetWaypoint = new Vector3(randomX, targetY, randomZ);

    }



    public void Die()

    {

        if (currentState == FishState.Dead) return;

        currentState = FishState.Dead;

        currentHealth = 0;

        customVelocity = Vector3.zero;

        transform.localEulerAngles = new Vector3(0f, 0f, 180f);

        Debug.Log($"<color=red>☠️ ปลา {gameObject.name} ตายแล้ว!</color>");

    }



    private void UpdateModelScale()

    {

        float scaleFactor = currentSize / 100f;

        transform.localScale = Vector3.one * scaleFactor;

    }



    private void OnDrawGizmosSelected()

    {

        if (targetWaypoint != Vector3.zero)

        {

            Gizmos.color = Color.yellow;

            Gizmos.DrawLine(transform.position, targetWaypoint);

            Gizmos.DrawSphere(targetWaypoint, 0.02f);

        }



        if (strategicWaypoint != Vector3.zero)

        {

            Gizmos.color = Color.red;

            Gizmos.DrawWireSphere(strategicWaypoint, schoolingSpread);

            Gizmos.DrawLine(transform.position, strategicWaypoint);

        }

    }



    protected virtual void ExecuteSpecialBehavior() { }

}



