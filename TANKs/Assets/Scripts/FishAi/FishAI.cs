using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FishAI : MonoBehaviour
{
    [Header("🧬 ข้อมูลสายพันธุ์")]
    public FishData data;
    public FishGender gender;

    [Header("📊 สถานะปัจจุบัน (Current Stats)")]
    public FishState currentState = FishState.Swimming;
    public float currentHealth = 100f;
    public float currentHunger = 100f;   // 0 = หิวตาย, 100 = อิ่ม
    public float currentMoisture = 100f; // ความชื้น (ลดเมื่ออยู่นอกน้ำ)
    public float currentMood = 100f;     // อารมณ์ (กระทบจากน้ำ/ความหิว)
    public int ageDays = 0;

    [Tooltip("ขนาดตัวปัจจุบันของปลา (หน่วย: เซนติเมตร)")]
    public float currentSize;

    [Header("⚙️ ตั้งค่าระบบนอกน้ำ")]
    [Tooltip("ความเร็วในการลดลงของความชื้นต่อวินาที ยิ่งน้อยยิ่งแห้งช้า")]
    public float moistureLossRate = 2.0f;

    private Vector3 targetWaypoint;
    private FishAI targetPrey;
    private float stateTimer = 0f;
    private float currentSpeedScale = 1f;
    private Rigidbody rb;
    private Renderer cachedSandRenderer; // ใช้ดึงขนาดตู้ที่แม่นยำ 100%

    private TankAIManager myManager;
    // ระบบฟิสิกส์จำลองด้วยโค้ด (Custom Code Physics)
    private Vector3 customVelocity;
    private bool isGrounded = false;
    [Header("⚙️ การป้องกัน")]
    public Collider tankCollider;
    private bool isObserving = false;    // เช็คว่าปลากำลังหยุดจ้องของขวางอยู่ไหม
    private float observeTimer = 0f;     // นาฬิกาจับเวลาตอนจ้อง
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // 🚨 ระบบหาตู้ปลาอัตโนมัติแบบชัวร์ๆ:
        if (tankCollider == null)
        {
            // 1. ลองหาจาก Tag
            GameObject tankObj = GameObject.FindWithTag("Tank");
            if (tankObj != null) tankCollider = tankObj.GetComponent<Collider>();

            // 2. ถ้ายังไม่เจอ ให้ Debug บอกเราใน Console
            if (tankCollider == null)
                Debug.LogWarning($"<color=yellow>⚠️ ปลา {gameObject.name} หาตู้ไม่เจอ! โปรดใส่ Tag'Tank' ให้ตู้ปลา หรือกำหนด Collider ให้ตัวแปร tankCollider ใน Inspector</color>");
        }

        currentSize = data.adultSize * 0.2f;
        gender = (Random.value > 0.5f) ? FishGender.Male : FishGender.Female;
        UpdateModelScale();

        myManager = GetComponentInParent<TankAIManager>();

        // ถ้าไม่เจอ ให้ค้นหาตัวที่ใกล้ที่สุด
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
        if (myManager == null || myManager.waterSim == null || cachedSandRenderer == null) return;

        // 1. ดึงค่า Local Y มาก่อน
        float localWaterY = myManager.waterSim.GetHeightAtWorldPos(transform.position);
        float localSandY = myManager.sandSim.GetHeightAtWorldPos(transform.position);

        // 2. แปลงให้เป็นพิกัดโลก (World Space) แบบเดียวกับที่ใช้ใน DropletController!
        float worldWaterY = myManager.waterSim.transform.TransformPoint(new Vector3(0, localWaterY, 0)).y;
        float worldSandY = myManager.sandSim.transform.TransformPoint(new Vector3(0, localSandY, 0)).y;

        Bounds b = cachedSandRenderer.bounds;
        bool isOutsideWalls = transform.position.x < b.min.x + 0.05f || transform.position.x > b.max.x - 0.05f ||
                              transform.position.z < b.min.z + 0.05f || transform.position.z > b.max.z - 0.05f;

        // 3. ปรับเงื่อนไขการเช็คน้ำให้ใจกว้างขึ้น
        // ถ้าระดับน้ำจริง (World Space) สูงกว่าพิกัด Y ของตัวปลา (บวกเผื่อไว้นิดนึง) ให้ถือว่าอยู่ในน้ำแล้ว
        bool isInWater = transform.position.y < (worldWaterY - 0.01f);

        if (isOutsideWalls || !isInWater)
        {
            if (currentState != FishState.Flopping)
            {
                currentState = FishState.Flopping;
                isGrounded = false;
            }

            currentMoisture -= moistureLossRate * Time.deltaTime;
            if (currentMoisture <= 0) Die();
        }
        else
        {
            // 4. ถ้าน้ำท่วมตัวปลากลับมาแล้ว สั่งให้เด้งตื่นขึ้นมาว่ายน้ำทันที!
            if (currentState == FishState.Flopping)
            {
                currentState = FishState.Swimming;
                isGrounded = true;
                customVelocity = Vector3.zero;

                // รีเซ็ตมุมตัวปลาให้ตั้งตรง (หายจากการนอนตะแคง)
                transform.localEulerAngles = new Vector3(0f, transform.localEulerAngles.y, 0f);

                // สั่งให้สุ่มจุดว่ายน้ำใหม่ทันที
                GetNewWaypoint();
            }
            currentMoisture = 100f; // รีเซ็ตความชุ่มชื้น
        }
    }
    private void ProcessStateMachine()
    {
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

                // 🚨 เซ็นเซอร์ตรวจจับการถูกอุ้ม (Player Grab Tool)
                if (isGrounded)
                {
                    float fishRadiusOffset = Mathf.Max(0.01f, (currentSize / 100f) * 0.25f);
                    if (!Physics.Raycast(transform.position, Vector3.down, fishRadiusOffset + 0.15f))
                    {
                        isGrounded = false; // ถูกย้ายไปกลางอากาศ สั่งให้ร่วงใหม่ทันที!
                        customVelocity = Vector3.zero;
                    }
                }

                if (!isGrounded) SimulateAirbornePhysics();
                break;
        }
    }

    private void SimulateAirbornePhysics()
    {
        customVelocity.y += -14f * Time.deltaTime;
        customVelocity.x *= Mathf.Exp(-1.5f * Time.deltaTime);
        customVelocity.z *= Mathf.Exp(-1.5f * Time.deltaTime);

        transform.position += customVelocity * Time.deltaTime;

        float fishRadiusOffset = Mathf.Max(0.01f, (currentSize / 100f) * 0.25f);
        float targetFloorY = -999f;
        bool hitValidGround = false;

        // 1. เช็คว่าร่วงอยู่ภายในขอบเขตตู้ปลาหรือไม่
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
            Vector3 worldSandPos =  myManager.sandSim.transform.TransformPoint(localPosSand);

            targetFloorY = worldSandPos.y;
            hitValidGround = true;
        }
        else
        {
            // 2. ร่วงนอกตู้ ยิงเรย์หาพื้นห้องจริงๆ
            float rayDistance = fishRadiusOffset + 0.1f;
            RaycastHit[] hits = Physics.RaycastAll(transform.position, Vector3.down, rayDistance);
            float closestDistance = float.MaxValue;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.gameObject == gameObject) continue;

                if (myManager != null)
                {
                    if (hit.collider.transform.IsChildOf(myManager.transform) ||
                        hit.collider.gameObject == myManager.gameObject)
                    {
                        continue;
                    }
                }

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

                if (customVelocity.y < -1.5f)
                {
                    customVelocity.y = -customVelocity.y * 0.15f;
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
        stateTimer += Time.deltaTime;

        // 🌟 แต่ละตัวสุ่มช่วงเวลาตัดสินใจไม่เท่ากัน ปลาจะไม่เปลี่ยนทิศทางพร้อมกัน
        float randomInterval = Random.Range(2.0f, 6.0f) * (1.0f / (data.hyperLevel / 50f + 0.5f));

        if (stateTimer > randomInterval)
        {
            stateTimer = 0f;
            // ปรับความเร็วตามขนาดตัว (ตัวใหญ่ให้เฉื่อยกว่า ตัวเล็กให้ปราดเปรียว)
            float sizeFactor = 1.0f / (currentSize / 10f); // ปลาใหญ่จะคูณความเร็วลดลง
            float hyperactivity = data.hyperLevel / 100f;

            float newTargetSpeed = Random.Range(0.3f, 0.9f) + (hyperactivity * 0.4f);
            currentSpeedScale = Mathf.Lerp(currentSpeedScale, newTargetSpeed * sizeFactor, 0.3f);
        }

        MoveTowardsTarget(data.baseSpeed * currentSpeedScale);
        // ...
    
        if (Vector3.Distance(transform.position, targetWaypoint) < 0.2f)
        {
            GetNewWaypoint();
        }
    }
    private void HandleForaging()
    {
        if (data.dietType == DietType.Carnivore)
        {
            if (targetPrey == null)
            {
                targetPrey = myManager.FindPreyFor(this);
            }

            if (targetPrey != null)
            {
                currentState = FishState.Attacking;
                return;
            }
        }
        MoveTowardsTarget(data.baseSpeed * 0.8f);
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
            if (dist > 0.5f) MoveTowards(targetPrey.transform.position, data.baseSpeed * 0.3f);
            else MoveTowards(targetPrey.transform.position, data.baseSpeed * 3.0f);
        }
        else if (data.huntingStyle == HuntStyle.Chase)
        {
            MoveTowards(targetPrey.transform.position, data.baseSpeed * 1.5f);
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

    private void CheckAggressionTrigger()
    {
        if (data.baseAggression + (100f - currentMood) > 120f)
        {
            targetPrey = myManager.FindPreyFor(this);
            if (targetPrey != null) currentState = FishState.Attacking;
        }
    }

    private void MoveTowardsTarget(float speed)
    {
        MoveTowards(targetWaypoint, speed);
    }

    private void MoveTowards(Vector3 pos, float speed)
    {
        // 🌟 เพิ่มความแปรปรวน (Variance) ให้ทิศทาง
        // ทำให้ปลาไม่ว่ายเป็นเส้นตรงเป๊ะๆ เหมือนหุ่นยนต์
        float varianceIntensity = 0.1f; // ปรับค่านี้ (0.1 - 0.3) ถ้ายิ่งมากปลายิ่งว่ายซิกแซก
        Vector3 noise = new Vector3(
            Mathf.Sin(Time.time * 0.5f) * varianceIntensity,
            Mathf.Cos(Time.time * 0.3f) * varianceIntensity,
            Mathf.Sin(Time.time * 0.7f) * varianceIntensity
        );

        Vector3 targetDir = ((pos + noise) - transform.position).normalized;

        if (targetDir != Vector3.zero && currentState != FishState.Flopping)
        {
            float lookAheadDistance = data.baseSpeed * 0.7f;
            float fishRadius = Mathf.Max(0.02f, currentSize / 100f);

            float currentMoveSpeed = speed;
            float turnSpeed = 2.5f;

            // 1. ตรวจสอบสถานะการจ้องมอง (Observing State)
            if (isObserving)
            {
                observeTimer -= Time.deltaTime;
                currentMoveSpeed = speed * 0.1f; // เบรกความเร็วเหลือ 10%

                if (observeTimer <= 0f)
                {
                    isObserving = false;
                    GetNewWaypoint();
                    return;
                }
            }
            else
            {
                // 2. เรดาร์สแกนสิ่งกีดขวาง
                if (Physics.SphereCast(transform.position, fishRadius, transform.forward, out RaycastHit hit, lookAheadDistance))
                {
                    bool hitWall = hit.collider.CompareTag("Tank");
                    bool hitObstacle = (myManager?.waterQuality?.tankObstacles.Contains(hit.collider) ?? false);

                    if (hitWall || hitObstacle)
                    {
                        isObserving = true;
                        observeTimer = Random.Range(1.0f, 1.5f);
                    }
                }
            }

            // 3. หมุนตัว (หันหนีไวขึ้นถ้ากำลังหลบสิ่งกีดขวาง)
            if (!isObserving && Vector3.Angle(transform.forward, targetDir) > 45f) turnSpeed = 4.5f;

            Quaternion targetRotation = Quaternion.LookRotation(targetDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);

            // 4. คำนวณการเคลื่อนที่
            float bobbing = Mathf.Sin(Time.time * 2f) * 0.005f;
            Vector3 movement = transform.forward * currentMoveSpeed * Time.deltaTime;
            movement.y += bobbing * Time.deltaTime * 2f;

            Vector3 nextPos = transform.position + movement;

            // 5. ป้องกันปลาว่ายทะลุพื้นทราย (Terrain Collision)
            if (myManager != null && myManager.sandSim != null)
            {
                float localSandY = myManager.sandSim.GetHeightAtWorldPos(nextPos);
                float worldSandY = myManager.sandSim.transform.TransformPoint(new Vector3(0, localSandY, 0)).y;

                if (nextPos.y < worldSandY + fishRadius)
                {
                    nextPos.y = worldSandY + fishRadius;
                    if (Random.value < 0.05f) { GetNewWaypoint(); isObserving = false; }
                }
            }
            if (!isObserving && Random.value < 0.002f)
            {
                GetNewWaypoint();
                return;
            }
            // 6. ป้องกันทะลุกระจก (Glass Collision)
            if (tankCollider != null)
            {
                Vector3 clampedPos = tankCollider.ClosestPoint(nextPos);
                if (Vector3.Distance(nextPos, clampedPos) > 0.005f)
                {
                    nextPos = clampedPos;
                    GetNewWaypoint();
                    isObserving = false;
                }
            }

            transform.position = nextPos;
        }
    }
    private void GetNewWaypoint()
    {
        if (tankCollider != null)
        {
            Bounds b = tankCollider.bounds;
            float randomX = Random.Range(b.min.x + 0.05f, b.max.x - 0.05f);
            float randomZ = Random.Range(b.min.z + 0.05f, b.max.z - 0.05f);
            float targetY = transform.position.y;

            if (myManager != null && myManager.waterSim != null && myManager.sandSim != null)
            {
                float worldWaterY = myManager.waterSim.transform.TransformPoint(new Vector3(0, myManager.waterSim.GetHeightAtWorldPos(transform.position), 0)).y;
                float worldSandY = myManager.sandSim.transform.TransformPoint(new Vector3(0, myManager.sandSim.GetHeightAtWorldPos(transform.position), 0)).y;

                switch (data.swimZone)
                {
                    case SwimZone.Bottom:
                        targetY = Random.Range(worldSandY + 0.02f, worldSandY + 0.15f);
                        break;
                    case SwimZone.Surface:
                        // 🌟 แก้ตรงนี้: เว้นระยะจากผิวน้ำลงมา 3-5 ซม. ปลาจะว่ายใต้ผิวน้ำนิ่งๆ ไม่พุ่งชนเพดาน
                        targetY = Random.Range(worldWaterY - 0.08f, worldWaterY - 0.03f);
                        break;
                    case SwimZone.Middle:
                        targetY = Random.Range(worldSandY + 0.15f, worldWaterY - 0.15f);
                        break;
                    case SwimZone.All:
                        targetY = Random.Range(worldSandY + 0.05f, worldWaterY - 0.05f);
                        break;
                }
            }
            targetWaypoint = new Vector3(randomX, targetY, randomZ);
        }
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

  
    protected virtual void ExecuteSpecialBehavior() { }
}