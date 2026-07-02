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
    public float currentHunger = 100f;
    public float currentMoisture = 100f;
    public float currentMood = 100f;
    public int ageDays = 0;
    public float currentSize;

    [Header("⚙️ ตั้งค่าเป้าหมาย (Waypoint Settings)")]
    public float targetReachDistance = 0.15f;
    public float maxWaypointTime = 5.0f;
    private float waypointTimer = 0f;

    [Header("⚙️ ตั้งค่าระบบนอกน้ำ")]
    public float moistureLossRate = 2.0f;

    [Header("🌊 ระบบรับรู้น้ำ (Water Detection)")]
    public float stateChangeDelay = 0.3f;
    public float baseWaterBuffer = 0.01f;
    public float sizeDepthMultiplier = 0.5f;

    [Header("🐟 ระบบฝูง (Schooling System)")]
    public FishAI myLeader; // ถ้าเป็นตัวเอง = เป็นจ่าฝูง
    public bool isLeader = false;

    [Header("🛠️ Tweakable AI Settings")]
    [Tooltip("ความถี่ในการเปลี่ยนจุดหมาย (วินาที)")]
    public float waypointInterval = 5.0f;

    [Tooltip("ระยะห่างระหว่างตัวปลาที่จะเริ่มผลักกัน (Separation)")]
    public float separationDistance = 0.25f;

    [Tooltip("แรงที่ปลาใช้ผลักกัน (ยิ่งมากยิ่งกระจายตัวแรง)")]
    public float separationPower = 1.5f;

    [Tooltip("ระยะห่างจากกระจกตู้ปลา (Wall Buffer)")]
    public float edgePadding = 0.15f;

    [Tooltip("ระยะกระจายตัวรอบๆ จ่าฝูง (กว้างแค่ไหน)")]
    public float schoolingSpread = 0.3f;

    [Tooltip("ระยะตรวจจับเพื่อนเพื่อรักษาระยะห่าง (กันปลาทับกัน)")]
    public float separationRadius = 0.25f;

    [Tooltip("แรงผลักกันเองเมื่ออยู่ใกล้เพื่อนเกินไป (ยิ่งเยอะยิ่งหนีกันแรง)")]
    public float separationStrength = 1.5f;
    [Tooltip("แรงผลักระหว่างต่างฝูง (ถ้าอยากให้ฝูงแยกกันชัดเจน ปรับค่านี้ให้มากกว่า Separation Strength)")]
    public float interSchoolRepulsion = 3.0f;
    private float uniqueOffset;

    [Header("⚙️ การเคลื่อนที่และการป้องกัน (Movement & Collision)")]
    public float rotationSpeed = 2.0f;
    public Collider tankCollider;

    [Tooltip("ตัวคูณระยะกันชนขอบตู้ ยิ่งน้อยยิ่งว่ายชิดกระจก (แนะนำ 0.5 - 1.0)")]
    public float wallBufferMultiplier = 1.0f;

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
    private bool isObserving = false;
    private float observeTimer = 0f;

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
        uniqueOffset = Random.Range(0f, 1000f);
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

        Invoke("InitializeSchooling", 0.5f);
    }

    private void InitializeSchooling()
    {
        FindSchool();
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

        if (myLeader == null) return;

        CheckTankBoundaries();
        ProcessStateMachine();
    }

    private void CheckTankBoundaries()
    {
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
        if (!isLeader && (myLeader == null || myLeader.currentState == FishState.Dead))
        {
            FindSchool();
            GetNewWaypoint();
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

    private void FindSchool()
    {
        if (myManager == null) return;

        isLeader = true;
        myLeader = this;

        List<FishAI> possibleLeaders = new List<FishAI>();

        foreach (FishAI fish in myManager.allFishInTank)
        {
            if (fish != this && fish.data != null && fish.data.speciesName == this.data.speciesName && fish.currentState != FishState.Dead)
            {
                if (fish.isLeader) possibleLeaders.Add(fish);
            }
        }

        foreach (FishAI leader in possibleLeaders)
        {
            int currentFollowers = 0;
            foreach (FishAI f in myManager.allFishInTank)
            {
                if (f.myLeader == leader) currentFollowers++;
            }

            if (currentFollowers < data.preferredSchoolSize)
            {
                isLeader = false;
                myLeader = leader;
                break;
            }
        }
    }

    private void SimulateAirbornePhysics()
    {
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
        MoveTowardsTarget(data.baseSpeed, false);

        waypointTimer -= Time.deltaTime;

        if (Vector3.Distance(transform.position, targetWaypoint) < targetReachDistance || waypointTimer <= 0f)
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

        MoveTowardsTarget(data.baseSpeed * 0.8f, false);

        waypointTimer -= Time.deltaTime;
        if (Vector3.Distance(transform.position, targetWaypoint) < targetReachDistance || waypointTimer <= 0f)
        {
            GetNewWaypoint();
        }
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
            if (dist > 0.5f) MoveTowards(targetPrey.transform.position, data.baseSpeed * 0.3f, false);
            else MoveTowards(targetPrey.transform.position, data.baseSpeed * 3.0f, true);
        }
        else if (data.huntingStyle == HuntStyle.Chase)
        {
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
        // 🌟 คำนวณแรงผลักแยกเป็น 2 ส่วน
        Vector3 separation = Vector3.zero;
        int count = 0;

        if (myManager != null && myManager.allFishInTank != null)
        {
            foreach (FishAI other in myManager.allFishInTank)
            {
                if (other != this && other.currentState != FishState.Dead)
                {
                    float dist = Vector3.Distance(transform.position, other.transform.position);
                    if (dist < separationRadius && dist > 0.001f)
                    {
                        // เช็คว่าเป็นฝูงเดียวกันไหม (ดูที่จ่าฝูง)
                        bool isSameSchool = (this.myLeader == other.myLeader);

                        // แรงผลัก: ถ้าฝูงเดียวกันใช้ separationStrength ถ้าคนละฝูงใช้ interSchoolRepulsion
                        float pushStrength = isSameSchool ? separationStrength : interSchoolRepulsion;

                        separation += (transform.position - other.transform.position).normalized * (1f - (dist / separationRadius)) * pushStrength;
                        count++;
                    }
                }
            }
        }
        if (count > 0) separation /= count;
        {
            separation = (separation / count) * separationStrength;
        }

        float varianceIntensity = 0.1f;
        Vector3 noise = new Vector3(
          Mathf.Sin((Time.time + uniqueOffset) * 0.5f) * varianceIntensity,
          Mathf.Cos((Time.time + uniqueOffset) * 0.3f) * varianceIntensity,
          Mathf.Sin((Time.time + uniqueOffset) * 0.7f) * varianceIntensity
        );

        Vector3 targetDir = ((pos + noise) - transform.position).normalized + separation;
        targetDir = targetDir.normalized;

        if (targetDir != Vector3.zero && currentState != FishState.Flopping)
        {
            float lookAheadDistance = maxSpeed * 0.7f;
            float fishRadius = Mathf.Max(0.02f, currentSize / 100f);

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

            float hyperFactor = data.hyperLevel / 100f;

            if (forceContinuous)
            {
                currentForwardSpeed = Mathf.Lerp(currentForwardSpeed, maxSpeed, Time.deltaTime * 5f);
            }
            else
            {
                swimPhaseTimer -= Time.deltaTime;

                if (isDarting)
                {
                    currentForwardSpeed = Mathf.Lerp(currentForwardSpeed, maxSpeed, Time.deltaTime * 4f);

                    if (swimPhaseTimer <= 0f)
                    {
                        isDarting = false;
                        swimPhaseTimer = Random.Range(1.0f, 4.0f) * (1.1f - hyperFactor);
                        if (hyperFactor > 0.8f) swimPhaseTimer *= 0.2f;
                    }
                }
                else
                {
                    float driftSpeed = -maxSpeed * 0.05f;
                    currentForwardSpeed = Mathf.Lerp(currentForwardSpeed, driftSpeed, Time.deltaTime * 2.5f);

                    if (swimPhaseTimer <= 0f)
                    {
                        isDarting = true;
                        swimPhaseTimer = Random.Range(0.5f, 1.5f) * (0.5f + (hyperFactor * 1.5f));
                    }
                }
            }

            float turnSpeed = rotationSpeed;
            if (!forceContinuous)
            {
                turnSpeed = isDarting ? rotationSpeed * 1.2f : rotationSpeed * 0.5f;
                turnSpeed *= Mathf.Lerp(0.5f, 1.5f, hyperFactor);
            }
            if (!isObserving && Vector3.Angle(transform.forward, targetDir) > 45f) turnSpeed *= 1.5f;

            Quaternion targetRotation = Quaternion.LookRotation(targetDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);

            float bobbing = Mathf.Sin((Time.time + uniqueOffset) * 2f) * 0.005f;
            if (currentForwardSpeed <= 0) bobbing *= 0.2f;

            Vector3 movement = transform.forward * currentForwardSpeed * Time.deltaTime;
            movement.y += bobbing * Time.deltaTime * 2f;
            Vector3 nextPos = transform.position + movement;

            // 🌟 2. ระบบล็อคแกน Y แบบดั้งเดิมที่เสถียร (ป้องกันการสั่น)
            float absoluteMinY = tankCollider != null ? tankCollider.bounds.min.y : -999f;
            float absoluteMaxY = tankCollider != null ? tankCollider.bounds.max.y : 999f;

            if (myManager != null && myManager.sandSim != null)
            {
                float localSandY = myManager.sandSim.GetHeightAtWorldPos(nextPos);
                float worldSandY = myManager.sandSim.transform.TransformPoint(new Vector3(0, localSandY, 0)).y;

                // ยึดค่าที่สูงกว่า ระหว่างก้นตู้เปล่าๆ กับพื้นทราย
                float currentFloorY = Mathf.Max(absoluteMinY, worldSandY);

                if (nextPos.y < currentFloorY + fishRadius)
                {
                    nextPos.y = currentFloorY + fishRadius;
                    if (Random.value < 0.05f) { GetNewWaypoint(); isObserving = false; }
                }

                if (myManager.waterSim != null)
                {
                    float localWaterY = myManager.waterSim.GetHeightAtWorldPos(nextPos);
                    float worldWaterY = myManager.waterSim.transform.TransformPoint(new Vector3(0, localWaterY, 0)).y;

                    // ยึดค่าที่ต่ำกว่า ระหว่างขอบบนตู้กับผิวน้ำ
                    float currentRoofY = Mathf.Min(absoluteMaxY, worldWaterY);

                    float maxAllowedY = currentRoofY - fishRadius;
                    if (maxAllowedY < currentFloorY + fishRadius) maxAllowedY = currentRoofY;

                    if (nextPos.y > maxAllowedY)
                    {
                        nextPos.y = maxAllowedY;
                        if (Random.value < 0.05f) { GetNewWaypoint(); isObserving = false; }
                    }
                }
            }
            else
            {
                // กรณีกันเหนียว ไม่มีระบบทราย/น้ำ ให้ล็อคตามขอบตู้เพียวๆ
                if (nextPos.y < absoluteMinY + fishRadius)
                {
                    nextPos.y = absoluteMinY + fishRadius;
                    if (Random.value < 0.05f) { GetNewWaypoint(); isObserving = false; }
                }
                else if (nextPos.y > absoluteMaxY - fishRadius)
                {
                    nextPos.y = absoluteMaxY - fishRadius;
                    if (Random.value < 0.05f) { GetNewWaypoint(); isObserving = false; }
                }
            }

            // 🌟 3. ล็อคแกน X และ Z แบบ Hard Clamp (กันทะลุกระจกข้าง 100%)
            if (tankCollider != null)
            {
                Bounds b = tankCollider.bounds;
                float wallBuffer = fishRadius * wallBufferMultiplier;

                float minX = b.min.x + wallBuffer;
                float maxX = b.max.x - wallBuffer;
                float minZ = b.min.z + wallBuffer;
                float maxZ = b.max.z - wallBuffer;

                bool hitGlass = false;

                if (nextPos.x < minX || nextPos.x > maxX)
                {
                    nextPos.x = Mathf.Clamp(nextPos.x, minX, maxX);
                    hitGlass = true;
                }

                if (nextPos.z < minZ || nextPos.z > maxZ)
                {
                    nextPos.z = Mathf.Clamp(nextPos.z, minZ, maxZ);
                    hitGlass = true;
                }

                if (hitGlass && Random.value < 0.2f)
                {
                    GetNewWaypoint();
                    isObserving = false;
                }
            }

            transform.position = nextPos;
        }
    }

    private void GetNewWaypoint()
    {
        if (tankCollider == null) return;

        waypointTimer = Random.Range(maxWaypointTime * 0.8f, maxWaypointTime * 1.2f);

        if (!isLeader && (myLeader == null || myLeader.currentState == FishState.Dead))
        {
            FindSchool();
        }

        Bounds b = tankCollider.bounds;
        float padding = 0.15f * wallBufferMultiplier;

        float randomX = 0f;
        float randomZ = 0f;

        if (isLeader)
        {
            float edgeZone = 0.3f;
            if (Random.value < 0.5f)
            {
                if (Random.value < 0.5f)
                {
                    randomX = (Random.value < 0.5f) ? Random.Range(b.min.x + padding, b.min.x + padding + edgeZone) : Random.Range(b.max.x - padding - edgeZone, b.max.x - padding);
                    randomZ = Random.Range(b.min.z + padding, b.max.z - padding);
                }
                else
                {
                    randomZ = (Random.value < 0.5f) ? Random.Range(b.min.z + padding, b.min.z + padding + edgeZone) : Random.Range(b.max.z - padding - edgeZone, b.max.z - padding);
                    randomX = Random.Range(b.min.x + padding, b.max.x - padding);
                }
            }
            else
            {
                randomX = Random.Range(b.min.x + padding + edgeZone, b.max.x - padding - edgeZone);
                randomZ = Random.Range(b.min.z + padding + edgeZone, b.max.z - padding - edgeZone);
            }
        }
        else
        {
            Vector3 leaderPos = myLeader.transform.position;
            Vector3 formationCenter = leaderPos + (myLeader.transform.forward * (schoolingSpread * 0.5f));

            randomX = Random.Range(formationCenter.x - schoolingSpread, formationCenter.x + schoolingSpread);
            randomZ = Random.Range(formationCenter.z - schoolingSpread, formationCenter.z + schoolingSpread);

            randomX = Mathf.Clamp(randomX, b.min.x + padding, b.max.x - padding);
            randomZ = Mathf.Clamp(randomZ, b.min.z + padding, b.max.z - padding);
        }

        float targetY = transform.position.y;

        if (myManager != null && myManager.waterSim != null && myManager.sandSim != null)
        {
            float localWaterY = myManager.waterSim.GetHeightAtWorldPos(new Vector3(randomX, 0, randomZ));
            float localSandY = myManager.sandSim.GetHeightAtWorldPos(new Vector3(randomX, 0, randomZ));
            float wy = myManager.waterSim.transform.TransformPoint(new Vector3(0, localWaterY, 0)).y;
            float sy = myManager.sandSim.transform.TransformPoint(new Vector3(0, localSandY, 0)).y;

            float absoluteMinY = tankCollider != null ? tankCollider.bounds.min.y : -999f;
            float absoluteMaxY = tankCollider != null ? tankCollider.bounds.max.y : 999f;

            float minY = Mathf.Max(sy, absoluteMinY) + 0.1f;
            float maxY = Mathf.Min(wy, absoluteMaxY) - 0.1f;

            if (minY > maxY) minY = maxY;

            switch (data.swimZone)
            {
                case SwimZone.Bottom: targetY = Random.Range(minY, Mathf.Min(minY + 0.2f, maxY)); break;
                case SwimZone.Surface: targetY = Random.Range(Mathf.Max(maxY - 0.2f, minY), maxY); break;
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

        isLeader = false;
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
            Gizmos.color = isLeader ? Color.yellow : Color.cyan;
            Gizmos.DrawLine(transform.position, targetWaypoint);
            Gizmos.DrawSphere(targetWaypoint, 0.02f);
        }
    }

    protected virtual void ExecuteSpecialBehavior() { }
}