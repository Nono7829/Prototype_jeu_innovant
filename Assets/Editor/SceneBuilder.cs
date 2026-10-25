using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// MMA > Build Full Scene : supprime puis reconstruit ring, ragdolls, gants, controle, camera, match.
public static class SceneBuilder
{
    // ---- Reglages de construction ----
    const float PlatformTop = 0.5f;     // hauteur de la plateforme
    const float Apothem = 3.0f;         // distance centre -> cote de l'octogone (MatchManager.apothem)
    const float SpawnHeight = 1.02f;    // hauteur du bassin au-dessus de la plateforme
    // Mets false si les coudes/genoux plient a l'envers apres la construction.
    const bool InvertXLimits = true;
    const float ElbowLow = -145f, ElbowHigh = 0f;
    const float KneeLow = 0f, KneeHigh = 130f;
    const float PlayerMassScale = 0.5f;

    [MenuItem("MMA/Build Full Scene")]
    static void BuildAll()
    {
        Clean();
        EnsureGround();
        BuildRing();

        GameObject player = BuildRagdoll("Player_Ragdoll", new Vector3(0f, PlatformTop, -1.3f), 0f, new Color(0.2f, 0.45f, 0.9f));
        GameObject opponent = BuildRagdoll("Opponent_Ragdoll", new Vector3(0f, PlatformTop, 0.5f), 180f, new Color(0.85f, 0.25f, 0.2f));
        RagdollBody playerBody = player.AddComponent<RagdollBody>();
        RagdollBody opponentBody = opponent.AddComponent<RagdollBody>();
        RagdollStabilizer stab = opponent.AddComponent<RagdollStabilizer>();
        stab.facingTarget = player.transform.Find("Pelvis");
        ApplyBodyPhysics(player, 0.15f, PlayerMassScale, "Body_Player_Mat");
        ApplyBodyPhysics(opponent, 0.10f, 0.2f, "Body_Opponent_Mat");

        // ----- Camera -----
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject g = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = g.AddComponent<Camera>();
            g.AddComponent<AudioListener>();
        }
        cam.transform.SetParent(null);
        GameObject rig = new GameObject("CameraRig");
        Undo.RegisterCreatedObjectUndo(rig, "Build Full Scene");
        GameObject pivot = NewChild("CameraPivot", rig.transform);
        StableCamera sc = cam.GetComponent<StableCamera>();
        if (sc == null) sc = cam.gameObject.AddComponent<StableCamera>();
        sc.pivot = pivot.transform; sc.player = playerBody; sc.opponent = opponentBody;

        // ----- Controle -----
        GameObject control = new GameObject("Player_Control");
        Undo.RegisterCreatedObjectUndo(control, "Build Full Scene");
        PlayerInputs inputs = control.AddComponent<PlayerInputs>();
        GloveTargets targets = control.AddComponent<GloveTargets>();
        Transform tL = NewChild("Target_L", control.transform).transform;
        Transform tR = NewChild("Target_R", control.transform).transform;
        Rigidbody shL = Bone(player, "Pelvis/Torso/UpperArm_L");
        Rigidbody shR = Bone(player, "Pelvis/Torso/UpperArm_R");
        targets.input = inputs; targets.frameReference = cam.transform;
        targets.shoulderL = shL; targets.shoulderR = shR;
        targets.targetL = tL; targets.targetR = tR;
        targets.floorY = PlatformTop;

        // ----- Gants -----
        GameObject gloves = new GameObject("Gloves");
        Undo.RegisterCreatedObjectUndo(gloves, "Build Full Scene");
        GameObject gL = CreateGlove("Glove_L", gloves.transform, player, "L", new Color(1f, 0.8f, 0.1f));
        GameObject gR = CreateGlove("Glove_R", gloves.transform, player, "R", new Color(0.2f, 0.9f, 0.3f));
        tL.position = gL.transform.position;
        tR.position = gR.transform.position;
        GloveGrab grabL = WireGlove(gL, 0, inputs, playerBody, tL, player, "L", gR.GetComponent<Collider>());
        GloveGrab grabR = WireGlove(gR, 1, inputs, playerBody, tR, player, "R", gL.GetComponent<Collider>());

        // ----- Gants adverses + IA -----
        GameObject oGloves = new GameObject("Opponent_Gloves");
        Undo.RegisterCreatedObjectUndo(oGloves, "Build Full Scene");
        GameObject ogL = CreateGlove("OppGlove_L", oGloves.transform, opponent, "L", new Color(0.95f, 0.5f, 0.1f));
        GameObject ogR = CreateGlove("OppGlove_R", oGloves.transform, opponent, "R", new Color(0.8f, 0.2f, 0.65f));
        GameObject ai = new GameObject("Opponent_AI");
        Undo.RegisterCreatedObjectUndo(ai, "Build Full Scene");
        Transform otL = NewChild("Target_L", ai.transform).transform;
        Transform otR = NewChild("Target_R", ai.transform).transform;
        otL.position = ogL.transform.position;
        otR.position = ogR.transform.position;
        WireOpponentGlove(ogL, otL, opponent, opponentBody, stab, ogR.GetComponent<Collider>(), "L");
        WireOpponentGlove(ogR, otR, opponent, opponentBody, stab, ogL.GetComponent<Collider>(), "R");
        OpponentBrain brain = ai.AddComponent<OpponentBrain>();
        brain.self = opponentBody; brain.stabilizer = stab; brain.player = playerBody;
        brain.shoulderL = Bone(opponent, "Pelvis/Torso/UpperArm_L");
        brain.shoulderR = Bone(opponent, "Pelvis/Torso/UpperArm_R");
        brain.targetL = otL; brain.targetR = otR;
        brain.playerGloveL = gL.GetComponent<Rigidbody>();
        brain.playerGloveR = gR.GetComponent<Rigidbody>();

        // ----- Match + debug -----
        GameObject game = new GameObject("_Game");
        Undo.RegisterCreatedObjectUndo(game, "Build Full Scene");
        MatchManager mm = game.AddComponent<MatchManager>();
        mm.player = playerBody; mm.opponent = opponentBody; mm.opponentStabilizer = stab;
        mm.gripL = grabL; mm.gripR = grabR;
        mm.apothem = Apothem; mm.platformTop = PlatformTop;
        DebugOverlay dbg = game.AddComponent<DebugOverlay>();
        dbg.player = playerBody;
        dbg.driverL = gL.GetComponent<GloveDriver>(); dbg.driverR = gR.GetComponent<GloveDriver>();
        dbg.grabL = grabL; dbg.grabR = grabR;

        AddSceneToBuild();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = control;
        Debug.Log("Scene construite. Sauvegarde (Ctrl+S) puis Play.");
    }

    // ================= Nettoyage =================

    static void Clean()
    {
        string[] names = { "Player_Ragdoll", "Opponent_Ragdoll", "Gloves", "Opponent_Gloves", "Opponent_AI", "Player_Control", "CameraRig", "Ring", "_Game", "_Debug" };
        foreach (string n in names)
        {
            GameObject go;
            while ((go = GameObject.Find(n)) != null) Object.DestroyImmediate(go);
        }
    }

    static void EnsureGround()
    {
        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
        }
        ground.GetComponent<MeshRenderer>().sharedMaterial = GetMaterial("Mat_Floor", new Color(0.12f, 0.13f, 0.16f));
        ground.GetComponent<Collider>().sharedMaterial = GetPhysicsMaterial();
    }

    static void AddSceneToBuild()
    {
        Scene s = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(s.path)) { Debug.LogWarning("Sauvegarde la scene (Ctrl+S) puis ajoute-la a File > Build Profiles."); return; }
        List<EditorBuildSettingsScene> list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (list.Exists(x => x.path == s.path)) return;
        list.Add(new EditorBuildSettingsScene(s.path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ================= Ring =================

    static void BuildRing()
    {
        GameObject ring = new GameObject("Ring");
        Undo.RegisterCreatedObjectUndo(ring, "Build Full Scene");

        GameObject platform = NewChild("Platform", ring.transform);
        platform.AddComponent<MeshFilter>().sharedMesh = SaveMesh(OctagonMesh(Apothem, PlatformTop));
        platform.AddComponent<MeshRenderer>().sharedMaterial = GetMaterial("Mat_Platform", new Color(0.30f, 0.33f, 0.42f));
        MeshCollider mc = platform.AddComponent<MeshCollider>();
        mc.sharedMesh = platform.GetComponent<MeshFilter>().sharedMesh;
        mc.convex = true;   // obligatoire : ClosestPoint (accroche des gants) exige un collider convexe
        mc.sharedMaterial = GetPhysicsMaterial();

        float r = Apothem / Mathf.Cos(22.5f * Mathf.Deg2Rad);
        Vector3[] corners = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            float a = (22.5f + 45f * i) * Mathf.Deg2Rad;
            corners[i] = new Vector3(r * Mathf.Cos(a), PlatformTop, r * Mathf.Sin(a));
        }

        Material postMat = GetMaterial("Mat_Post", new Color(0.15f, 0.15f, 0.15f));
        Material ropeMat = GetMaterial("Mat_Rope", new Color(0.9f, 0.15f, 0.15f));
        foreach (Vector3 c in corners)
        {
            GameObject post = Cylinder("Post", ring.transform, c + Vector3.up * 0.55f, Quaternion.identity, new Vector3(0.12f, 0.55f, 0.12f), postMat);
            Object.DestroyImmediate(post.GetComponent<Collider>());
        }

        float[] ropeHeights = { 0.35f, 0.55f, 0.75f };   // espaces de moins que l'epaisseur d'un corps couche
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = corners[i], q = corners[(i + 1) % 8];
            Vector3 dir = q - p;
            foreach (float h in ropeHeights)
            {
                GameObject rope = Cylinder("Rope", ring.transform, (p + q) * 0.5f + Vector3.up * h,
                    Quaternion.FromToRotation(Vector3.up, dir.normalized), new Vector3(0.07f, dir.magnitude * 0.5f, 0.07f), ropeMat);
                rope.AddComponent<GrabSurface>();   // elastic = true par defaut
            }
        }
    }

    static GameObject Cylinder(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.SetPositionAndRotation(pos, rot);
        g.transform.localScale = scale;
        g.GetComponent<MeshRenderer>().sharedMaterial = m;
        return g;
    }

    static Mesh OctagonMesh(float apothem, float height)
    {
        float r = apothem / Mathf.Cos(22.5f * Mathf.Deg2Rad);
        Vector3[] top = new Vector3[8], bot = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            float a = (22.5f + 45f * i) * Mathf.Deg2Rad;
            top[i] = new Vector3(r * Mathf.Cos(a), height, r * Mathf.Sin(a));
            bot[i] = new Vector3(top[i].x, 0f, top[i].z);
        }
        List<Vector3> v = new List<Vector3>();
        List<int> t = new List<int>();
        for (int i = 0; i < 8; i++)
        {
            int j = (i + 1) % 8;
            Tri(v, t, new Vector3(0f, height, 0f), top[i], top[j], Vector3.up);
            Tri(v, t, Vector3.zero, bot[i], bot[j], Vector3.down);
            Vector3 n = (top[i] + top[j]) * 0.5f; n.y = 0f; n.Normalize();
            Tri(v, t, bot[i], top[i], top[j], n);
            Tri(v, t, bot[i], top[j], bot[j], n);
        }
        Mesh m = new Mesh { name = "OctagonPlatform" };
        m.SetVertices(v);
        m.SetTriangles(t, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // Ajoute un triangle en corrigeant l'ordre pour que sa face visible regarde vers "outward".
    static void Tri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) { Vector3 tmp = b; b = c; c = tmp; }
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(c);
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }

    static Mesh SaveMesh(Mesh m)
    {
        EnsureFolder("Assets/Models");
        string path = "Assets/Models/OctagonPlatform.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    // ================= Ragdoll =================

    static GameObject BuildRagdoll(string rootName, Vector3 position, float yaw, Color color)
    {
        Material bodyMat = GetMaterial("Mat_" + rootName, color);
        Material markerMat = GetMaterial("Mat_Marker", Color.white);

        GameObject root = new GameObject(rootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Full Scene");
        root.transform.position = position;

        Rigidbody pelvis = AddBone("Pelvis", root.transform, new Vector3(0f, SpawnHeight, 0f), 12f);
        AddBox(pelvis.transform, new Vector3(0.32f, 0.20f, 0.20f), Vector3.zero, bodyMat);

        Rigidbody torso = AddBone("Torso", pelvis.transform, new Vector3(0f, 0.10f, 0f), 22f);
        AddBox(torso.transform, new Vector3(0.34f, 0.45f, 0.22f), new Vector3(0f, 0.22f, 0f), bodyMat);
        AddJoint(torso, pelvis, -20f, 45f, 30f, 25f, 3f);

        Rigidbody head = AddBone("Head", torso.transform, new Vector3(0f, 0.44f, 0f), 5f);
        AddSphere(head.transform, 0.11f, new Vector3(0f, 0.13f, 0f), bodyMat);
        AddVisual(PrimitiveType.Sphere, head.transform, new Vector3(0f, 0.13f, 0.10f), Vector3.one * 0.05f, markerMat); // "nez" = avant
        AddJoint(head, torso, -40f, 40f, 50f, 30f, 0.5f);

        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1f : 1f;
            string side = i == 0 ? "L" : "R";

            Rigidbody upperArm = AddBone("UpperArm_" + side, torso.transform, new Vector3(s * 0.24f, 0.40f, 0f), 2.5f);
            AddCapsule(upperArm.transform, 0.05f, 0.30f, new Vector3(0f, -0.15f, 0f), bodyMat);
            AddJoint(upperArm, torso, -150f, 60f, 80f, 100f, 0.8f);

            Rigidbody forearm = AddBone("Forearm_" + side, upperArm.transform, new Vector3(0f, -0.30f, 0f), 1.5f);
            AddCapsule(forearm.transform, 0.045f, 0.27f, new Vector3(0f, -0.135f, 0f), bodyMat);
            AddJoint(forearm, upperArm, ElbowLow, ElbowHigh, 20f, 0f, 0.2f);

            Rigidbody hand = AddBone("Hand_" + side, forearm.transform, new Vector3(0f, -0.27f, 0f), 0.6f);
            hand.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            AddSphere(hand.transform, 0.055f, new Vector3(0f, -0.055f, 0f), bodyMat);
            AddJoint(hand, forearm, -50f, 50f, 15f, 30f, 0.03f);

            Rigidbody thigh = AddBone("Thigh_" + side, pelvis.transform, new Vector3(s * 0.10f, -0.08f, 0f), 7f);
            AddCapsule(thigh.transform, 0.075f, 0.45f, new Vector3(0f, -0.225f, 0f), bodyMat);
            AddJoint(thigh, pelvis, -90f, 30f, 40f, 40f, 2f);

            Rigidbody shin = AddBone("Shin_" + side, thigh.transform, new Vector3(0f, -0.45f, 0f), 3.5f);
            AddCapsule(shin.transform, 0.06f, 0.40f, new Vector3(0f, -0.20f, 0f), bodyMat);
            AddJoint(shin, thigh, KneeLow, KneeHigh, 10f, 0f, 1f);

            Rigidbody foot = AddBone("Foot_" + side, shin.transform, new Vector3(0f, -0.40f, 0f), 1.2f);
            AddBox(foot.transform, new Vector3(0.10f, 0.07f, 0.26f), new Vector3(0f, -0.035f, 0.06f), bodyMat);
            AddJoint(foot, shin, -30f, 40f, 10f, 15f, 0.3f);
        }

        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        return root;
    }

    static Rigidbody AddBone(string name, Transform parent, Vector3 localPos, float mass)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.1f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rb.maxAngularVelocity = 20f;
        rb.maxDepenetrationVelocity = 3f;
        return rb;
    }

    static void AddJoint(Rigidbody body, Rigidbody parent, float xLow, float xHigh, float yLimit, float zLimit, float damper)
    {
        ConfigurableJoint j = body.gameObject.AddComponent<ConfigurableJoint>();
        j.connectedBody = parent;
        j.axis = Vector3.right;
        j.secondaryAxis = Vector3.up;
        j.anchor = Vector3.zero;
        j.autoConfigureConnectedAnchor = true;
        j.enableCollision = false;
        j.xMotion = ConfigurableJointMotion.Locked;
        j.yMotion = ConfigurableJointMotion.Locked;
        j.zMotion = ConfigurableJointMotion.Locked;
        j.angularXMotion = ConfigurableJointMotion.Limited;
        j.angularYMotion = yLimit > 0f ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked;
        j.angularZMotion = zLimit > 0f ? ConfigurableJointMotion.Limited : ConfigurableJointMotion.Locked;

        float low = InvertXLimits ? -xHigh : xLow;
        float high = InvertXLimits ? -xLow : xHigh;
        j.lowAngularXLimit = new SoftJointLimit { limit = low };
        j.highAngularXLimit = new SoftJointLimit { limit = high };
        j.angularYLimit = new SoftJointLimit { limit = yLimit };
        j.angularZLimit = new SoftJointLimit { limit = zLimit };

        j.projectionMode = JointProjectionMode.PositionAndRotation;
        j.projectionDistance = 0.1f;
        j.projectionAngle = 180f;

        // Frottement articulaire pur : ressort 0, amortisseur > 0 (le stabilisateur de l'adversaire le module)
        j.rotationDriveMode = RotationDriveMode.Slerp;
        j.slerpDrive = new JointDrive { positionSpring = 0f, positionDamper = damper, maximumForce = float.MaxValue };
    }

    static void AddCapsule(Transform bone, float radius, float height, Vector3 center, Material mat)
    {
        CapsuleCollider c = bone.gameObject.AddComponent<CapsuleCollider>();
        c.direction = 1; c.radius = radius; c.height = height; c.center = center;
        AddVisual(PrimitiveType.Capsule, bone, center, new Vector3(radius * 2f, height / 2f, radius * 2f), mat);
    }

    static void AddBox(Transform bone, Vector3 size, Vector3 center, Material mat)
    {
        BoxCollider c = bone.gameObject.AddComponent<BoxCollider>();
        c.size = size; c.center = center;
        AddVisual(PrimitiveType.Cube, bone, center, size, mat);
    }

    static void AddSphere(Transform bone, float radius, Vector3 center, Material mat)
    {
        SphereCollider c = bone.gameObject.AddComponent<SphereCollider>();
        c.radius = radius; c.center = center;
        AddVisual(PrimitiveType.Sphere, bone, center, Vector3.one * radius * 2f, mat);
    }

    static GameObject AddVisual(PrimitiveType type, Transform parent, Vector3 center, Vector3 scale, Material mat)
    {
        GameObject v = GameObject.CreatePrimitive(type);
        v.name = "Visual";
        Object.DestroyImmediate(v.GetComponent<Collider>());
        v.transform.SetParent(parent, false);
        v.transform.localPosition = center;
        v.transform.localScale = scale;
        v.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return v;
    }

    // ================= Gants =================

    static Rigidbody Bone(GameObject root, string path)
    {
        Transform t = root.transform.Find(path);
        return t != null ? t.GetComponent<Rigidbody>() : null;
    }

    static GameObject CreateGlove(string name, Transform parent, GameObject player, string side, Color color)
    {
        Transform hand = player.transform.Find("Pelvis/Torso/UpperArm_" + side + "/Forearm_" + side + "/Hand_" + side);
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = hand.TransformPoint(hand.GetComponent<SphereCollider>().center);

        Rigidbody rb = g.AddComponent<Rigidbody>();
        rb.mass = 1.2f;
        rb.useGravity = false;
        rb.linearDamping = 0f;
        rb.angularDamping = 2f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.maxAngularVelocity = 20f;
        rb.maxDepenetrationVelocity = 3f;

        g.AddComponent<SphereCollider>().radius = 0.10f;
        AddVisual(PrimitiveType.Sphere, g.transform, Vector3.zero, Vector3.one * 0.20f, GetMaterial("Mat_" + name, color));
        return g;
    }

    static GloveGrab WireGlove(GameObject glove, int index, PlayerInputs inputs, RagdollBody body,
                               Transform target, GameObject player, string side, Collider otherGlove)
    {
        GloveDriver d = glove.AddComponent<GloveDriver>();
        d.glove = glove.GetComponent<Rigidbody>();
        d.target = target;
        d.upperArm = Bone(player, "Pelvis/Torso/UpperArm_" + side);
        d.forearm = Bone(player, "Pelvis/Torso/UpperArm_" + side + "/Forearm_" + side);
        d.hand = Bone(player, "Pelvis/Torso/UpperArm_" + side + "/Forearm_" + side + "/Hand_" + side);
        d.body = body;
        d.otherGlove = otherGlove;

        GloveGrab grab = glove.AddComponent<GloveGrab>();
        grab.input = inputs;
        grab.handIndex = index;
        grab.ownBody = body;
        grab.visual = glove.transform.Find("Visual");
        d.grab = grab;

        glove.AddComponent<GloveImpact>();
        return grab;
    }

    static void WireOpponentGlove(GameObject glove, Transform target, GameObject opp, RagdollBody body,
                              RagdollStabilizer stab, Collider otherGlove, string side)
    {
        GloveDriver d = glove.AddComponent<GloveDriver>();
        d.glove = glove.GetComponent<Rigidbody>();
        d.target = target;
        d.upperArm = Bone(opp, "Pelvis/Torso/UpperArm_" + side);
        d.forearm = Bone(opp, "Pelvis/Torso/UpperArm_" + side + "/Forearm_" + side);
        d.hand = Bone(opp, "Pelvis/Torso/UpperArm_" + side + "/Forearm_" + side + "/Hand_" + side);
        d.body = body;
        d.otherGlove = otherGlove;
        d.powerSource = stab;
        d.freeReaction = 0.05f;
        glove.AddComponent<GloveImpact>();
    }

    static void ApplyBodyPhysics(GameObject root, float friction, float massScale, string materialName)
    {
        string path = "Assets/Materials/" + materialName + ".asset";
        PhysicsMaterial pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (pm == null)
        {
            EnsureFolder("Assets/Materials");
            pm = new PhysicsMaterial(materialName)
            {
                dynamicFriction = friction,
                staticFriction = friction,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,   // l'emporte sur le sol (Average)
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            AssetDatabase.CreateAsset(pm, path);
        }
        foreach (Collider c in root.GetComponentsInChildren<Collider>()) c.sharedMaterial = pm;
        foreach (Rigidbody rb in root.GetComponentsInChildren<Rigidbody>()) rb.mass *= massScale;
    }

    // ================= Utilitaires =================

    static GameObject NewChild(string name, Transform parent)
    {
        GameObject g = new GameObject(name);
        g.transform.SetParent(parent, false);
        return g;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        AssetDatabase.CreateFolder("Assets", path.Substring("Assets/".Length));
    }

    static Material GetMaterial(string name, Color color)
    {
        string path = "Assets/Materials/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        EnsureFolder("Assets/Materials");
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        mat = new Material(shader) { color = color };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static PhysicsMaterial GetPhysicsMaterial()
    {
        const string path = "Assets/Materials/Platform_Mat.asset";
        PhysicsMaterial pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (pm != null) return pm;
        EnsureFolder("Assets/Materials");
        pm = new PhysicsMaterial("Platform_Mat")
        {
            dynamicFriction = 0.7f,
            staticFriction = 0.7f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Average,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        AssetDatabase.CreateAsset(pm, path);
        return pm;
    }
}