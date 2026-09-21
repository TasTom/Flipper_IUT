using UnityEngine;
using UnityEditor;
using System.IO;

public class BuildPinballScene
{
    [MenuItem("Tools/Build Pinball Scene")]
    public static void BuildScene()
    {
        // Create a new empty GameObject to hold the table
        GameObject tableRoot = new GameObject("PinballTable");
        tableRoot.transform.position = Vector3.zero;

        // Create the playfield (table base)
        GameObject playfield = GameObject.CreatePrimitive(PrimitiveType.Cube);
        playfield.transform.SetParent(tableRoot.transform);
        playfield.transform.localPosition = new Vector3(0, 0, 0);
        playfield.transform.localScale = new Vector3(4f, 0.1f, 20f); // Approximate size
        playfield.name = "Playfield";
        // Make it a static object
        playfield.isStatic = true;

        // Create flippers
        GameObject leftFlipper = CreateFlipper("LeftFlipper", new Vector3(-1.5f, 0.05f, -8f), -90f);
        GameObject rightFlipper = CreateFlipper("RightFlipper", new Vector3(1.5f, 0.05f, -8f), 90f);
        leftFlipper.transform.SetParent(tableRoot.transform);
        rightFlipper.transform.SetParent(tableRoot.transform);

        // Create plunger
        GameObject plunger = CreatePlunger(new Vector3(2.5f, 0.05f, -10f));
        plunger.transform.SetParent(tableRoot.transform);

        // Create bumpers
        CreateBumper(new Vector3(-2f, 0.05f, 0f), tableRoot.transform);
        CreateBumper(new Vector3(0f, 0.05f, 2f), tableRoot.transform);
        CreateBumper(new Vector3(2f, 0.05f, 0f), tableRoot.transform);

        // Create slingshots
        CreateSlingshot(new Vector3(-2.5f, 0.05f, -6f), tableRoot.transform);
        CreateSlingshot(new Vector3(2.5f, 0.05f, -6f), tableRoot.transform);

        // Create drain zone
        GameObject drainZone = GameObject.CreatePrimitive(PrimitiveType.Cube);
        drainZone.transform.SetParent(tableRoot.transform);
        drainZone.transform.localPosition = new Vector3(0, -0.1f, -10f);
        drainZone.transform.localScale = new Vector3(5f, 0.2f, 2f);
        drainZone.name = "DrainZone";
        // Make it a trigger
        var col = drainZone.GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
        drainZone.AddComponent<DrainZoneScript>();

        // Create walls
        CreateWall(new Vector3(0, 0.5f, 10f), new Vector3(4f, 1f, 0.1f), tableRoot.transform); // top wall
        CreateWall(new Vector3(-2f, 0.5f, 0f), new Vector3(0.1f, 1f, 4f), tableRoot.transform); // left wall
        CreateWall(new Vector3(2f, 0.5f, 0f), new Vector3(0.1f, 1f, 4f), tableRoot.transform); // right wall

        // Center the table
        tableRoot.transform.position = new Vector3(0, 0, 0);

        Debug.Log("Pinball scene built successfully.");
    }

    private static GameObject CreateFlipper(string name, Vector3 position, float rotationZ)
    {
        GameObject flipper = new GameObject(name);
        flipper.transform.position = position;
        flipper.transform.rotation = Quaternion.Euler(0, 0, rotationZ);

        // Create the flipper bat (a scaled cube)
        GameObject bat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bat.transform.SetParent(flipper.transform);
        bat.transform.localPosition = new Vector3(0, 0, 0.5f); // Adjust based on flipper length
        bat.transform.localScale = new Vector3(0.2f, 0.1f, 1.5f); // Adjust size
        bat.name = "Bat";

        // Add a Rigidbody to the bat
        Rigidbody rb = bat.AddComponent<Rigidbody>();
        rb.mass = 0.5f;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.1f;

        // Add a Collider to the bat (already has one from CreatePrimitive, but we can adjust)
        var col = bat.GetComponent<Collider>();
        if (col != null)
        {
            col.material = new PhysicsMaterial { bounciness = 0.3f, frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = PhysicsMaterialCombine.Maximum };
        }

        // Create a hinge joint for the flipper
        HingeJoint hinge = flipper.AddComponent<HingeJoint>();
        hinge.anchor = Vector3.zero;
        hinge.axis = Vector3.forward; // Assuming the flipper rotates around the Z-axis
        hinge.useSpring = true;
        var spring = hinge.spring;
        spring.spring = 5000f;
        spring.damper = 100f;
        spring.targetPosition = 0f; // Rest position
        hinge.spring = spring;

        // Add limits to the hinge
        hinge.useLimits = true;
        var limits = hinge.limits;
        limits.min = -30f; // Adjust based on flipper angle
        limits.max = 30f;
        hinge.limits = limits;

        return flipper;
    }

    private static GameObject CreatePlunger(Vector3 position)
    {
        GameObject plunger = new GameObject("Plunger");
        plunger.transform.position = position;

        // Create the plunger rod
        GameObject rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rod.transform.SetParent(plunger.transform);
        rod.transform.localPosition = new Vector3(0, 0.5f, 0);
        rod.transform.localScale = new Vector3(0.1f, 1f, 0.1f);
        rod.name = "Rod";

        // Add a Rigidbody to the rod
        Rigidbody rb = rod.AddComponent<Rigidbody>();
        rb.mass = 0.2f;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.1f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ | RigidbodyConstraints.FreezePositionY;

        // Add a plunger script
        plunger.AddComponent<PlungerScript>();

        return plunger;
    }

    private static void CreateBumper(Vector3 position, Transform parent)
    {
        GameObject bumper = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bumper.transform.position = position;
        bumper.transform.SetParent(parent);
        bumper.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        bumper.name = "Bumper";

        // Add a Rigidbody
        Rigidbody rb = bumper.AddComponent<Rigidbody>();
        rb.mass = 0.5f;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.1f;

        // Add a bouncy material
        var col = bumper.GetComponent<Collider>();
        if (col != null)
        {
            col.material = new PhysicsMaterial { bounciness = 0.8f, frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Maximum };
        }

        // Add a bumper script
        bumper.AddComponent<BumperScript>();
    }

    private static void CreateSlingshot(Vector3 position, Transform parent)
    {
        GameObject slingshot = new GameObject("Slingshot");
        slingshot.transform.position = position;
        slingshot.transform.SetParent(parent);

        // Create two arms
        GameObject leftArm = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leftArm.transform.SetParent(slingshot.transform);
        leftArm.transform.localPosition = new Vector3(-0.3f, 0, 0);
        leftArm.transform.localScale = new Vector3(0.1f, 0.2f, 0.5f);
        leftArm.name = "LeftArm";

        GameObject rightArm = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightArm.transform.SetParent(slingshot.transform);
        rightArm.transform.localPosition = new Vector3(0.3f, 0, 0);
        rightArm.transform.localScale = new Vector3(0.1f, 0.2f, 0.5f);
        rightArm.name = "RightArm";

        // Add springs to the arms
        foreach (Transform arm in slingshot.transform)
        {
            var springJoint = arm.gameObject.AddComponent<SpringJoint>();
            springJoint.connectedBody = slingshot.AddComponent<Rigidbody>();
            springJoint.spring = 5000f;
            springJoint.damper = 100f;
            springJoint.minDistance = 0.1f;
            springJoint.maxDistance = 0.5f;
        }

        // Add a slingshot script
        slingshot.AddComponent<SlingshotScript>();
    }

    private static void CreateWall(Vector3 position, Vector3 scale, Transform parent)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = position;
        wall.transform.SetParent(parent);
        wall.transform.localScale = scale;
        wall.name = "Wall";
        wall.isStatic = true;
    }
}

// Dummy scripts for the components
public class DrainZoneScript : MonoBehaviour { }
public class PlungerScript : MonoBehaviour { }
public class BumperScript : MonoBehaviour { }
public class SlingshotScript : MonoBehaviour { }
