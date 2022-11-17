using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Utilities;
using UnityEngine.UI;

public class AvatarManager : MonoBehaviour
{

    private int currentSkinTone = 0;
    private int currentHairColor = 0;

    public Image baseImage;
    public Image eyeImage;
    public Image browImage;
    public Image noseImage;
    public Image mouthImage;
    public Image hairImage;

    public Sprite[] bases = new Sprite[3]; //0 light, 1 med, 2 dark

    public Sprite[] LightEyes = new Sprite[3];
    public Sprite[] MidEyes = new Sprite[3]; 
    public Sprite[] DarkEyes = new Sprite[3];

    public Sprite[] LightBrows = new Sprite[3];
    public Sprite[] MidBrows = new Sprite[3];
    public Sprite[] DarkBrows = new Sprite[3];

    public Sprite[] LightNoses = new Sprite[3];
    public Sprite[] MidNoses = new Sprite[3];
    public Sprite[] DarkNoses = new Sprite[3];

    public Sprite[] LightMouths = new Sprite[3];
    public Sprite[] MidMouths = new Sprite[3];
    public Sprite[] DarkMouths = new Sprite[3];

    public Sprite[] LightHair = new Sprite[3];
    public Sprite[] MidHair = new Sprite[3];
    public Sprite[] DarkHair = new Sprite[3];

    private SerializableDictionary<int, Sprite> eyes = new SerializableDictionary<int, Sprite>(); //tone X variant
    private Sprite[][] eyesArray = new Sprite[3][]; //tone X variant
    private int currentEyeIndex = 0;
    private Sprite[][] browsArray = new Sprite[3][]; //tone X variant
    private int currentBrowIndex = 0;
    private Sprite[][] noseArray = new Sprite[3][]; //tone X variant
    private int currentNoseIndex = 0;
    private Sprite[][] mouthArray = new Sprite[3][]; //tone X variant
    private int currentMouthIndex = 0;
    private Sprite[][] hairArray = new Sprite[3][]; //tone X variant
    private int currentHairIndex = 0;

    // Start is called before the first frame update
    void Start()
    {
        eyesArray[0] = LightEyes;
        eyesArray[1] = MidEyes;
        eyesArray[2] = DarkEyes;
        browsArray[0] = LightBrows;
        browsArray[1] = MidBrows;
        browsArray[2] = DarkBrows;
        noseArray[0] = LightNoses;
        noseArray[1] = MidNoses;
        noseArray[2] = DarkNoses;
        mouthArray[0] = LightMouths;
        mouthArray[1] = MidMouths;
        mouthArray[2] = DarkMouths;
        hairArray[0] = LightHair;
        hairArray[1] = MidHair;
        hairArray[2] = DarkHair;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetSkinTone(int tone)
    {
        currentSkinTone = tone;
        baseImage.sprite = bases[tone];
        eyeImage.sprite = eyesArray[tone][currentEyeIndex];
        browImage.sprite = browsArray[tone][currentBrowIndex];
        noseImage.sprite = noseArray[tone][currentNoseIndex];
        mouthImage.sprite = mouthArray[tone][currentMouthIndex];
    }

    public void SetHairColor(int color){
        currentHairColor = color;
        hairImage.sprite = hairArray[currentSkinTone][currentHairIndex];
    }

    public void ChangeEyes(int direction){
        currentEyeIndex += direction;
        if(currentEyeIndex == -1){ currentEyeIndex = eyesArray[currentSkinTone].Length - 1;}
        currentEyeIndex = currentEyeIndex % eyesArray[currentSkinTone].Length;
        eyeImage.sprite = eyesArray[currentSkinTone][currentEyeIndex];
    }
    public void ChangeBrows(int direction){
        currentBrowIndex += direction;
        if(currentBrowIndex == -1){ currentBrowIndex = browsArray[currentSkinTone].Length - 1;}
        currentBrowIndex = currentBrowIndex % browsArray[currentSkinTone].Length;
        browImage.sprite = browsArray[currentSkinTone][currentBrowIndex];
    }
    public void ChangeNose(int direction){
        currentNoseIndex += direction;
        if(currentNoseIndex == -1){ currentNoseIndex = noseArray[currentSkinTone].Length - 1;}
        currentNoseIndex = currentNoseIndex % noseArray[currentSkinTone].Length;
        noseImage.sprite = noseArray[currentSkinTone][currentNoseIndex];
    }
    public void ChangeMouth(int direction){
        currentMouthIndex += direction;
        if(currentMouthIndex == -1){ currentMouthIndex = mouthArray[currentSkinTone].Length - 1;}
        currentMouthIndex = currentMouthIndex % mouthArray[currentSkinTone].Length;
        mouthImage.sprite = mouthArray[currentSkinTone][currentMouthIndex];
    }
    public void ChangeHair(int direction){
        currentHairIndex += direction;
        if(currentHairIndex == -1){ currentHairIndex = hairArray[currentHairColor].Length - 1;}
        currentHairIndex = currentHairIndex % hairArray[currentHairColor].Length;
        hairImage.sprite = mouthArray[currentHairColor][currentHairIndex];
    }


}
