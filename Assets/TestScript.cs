using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TestScript : MonoBehaviour
{
    public GalaxyController galaxyController;
    bool wasOpened = false;
    float accelerometerUpdateInterval = 1.0f / 60.0f;
    // The greater the value of LowPassKernelWidthInSeconds, the slower the
    // filtered value will converge towards current input sample (and vice versa).
    float lowPassKernelWidthInSeconds = 1.0f;
    // This next parameter is initialized to 2.0 per Apple's recommendation,
    // or at least according to Brady! ;)
    float shakeDetectionThreshold = 2.0f;

    float lowPassFilterFactor;
    Vector3 lowPassValue;

    public Image avatarImage;

    // Start is called before the first frame update
    void Start()
    {
        galaxyController.GetPlayerAvatarTexture((imageTexture) =>{
            avatarImage.sprite = Sprite.Create(imageTexture, new Rect(0.0f, 0.0f, imageTexture.width, imageTexture.height), Vector2.one * 0.5f);
        });
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown("space"))
        {
            galaxyController.ShowLeaderboard();
            print("space key was pressed");
        }

        if (Input.GetKeyDown("b"))
        {
            galaxyController.ShowLeaderboard("", 0, 100, 25, 10);
            print("space key was pressed");
        }

        if (Input.GetKeyDown("a"))
        {
            Contacts.LoadContactList();
            print("a key was pressed");
        }

        Vector3 acceleration = Input.acceleration;
        lowPassValue = Vector3.Lerp(lowPassValue, acceleration, lowPassFilterFactor);
        Vector3 deltaAcceleration = acceleration - lowPassValue;

        if (deltaAcceleration.sqrMagnitude >= shakeDetectionThreshold)
        {
            controller.ShowLeaderboard();
            wasOpened = true;
            Debug.Log("Shake event detected at time "+Time.time);
        }

    }

}
