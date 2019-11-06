using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MuseIntegrationExample : MonoBehaviour
{
    public MuseListener.Band band;
    public Slider slider;
    public Renderer cube;
    public Text text;

    MuseListener museListener;

    private void Awake()
    {
        museListener = FindObjectOfType<MuseListener>();
    }

    void Start()
    {
        
    }

    void Update()
    {
        float brainWaveScore = museListener.GetBrainWave(band).Score;
        slider.value = brainWaveScore;
        cube.material.color = Color.Lerp(Color.black, Color.white, brainWaveScore);
        text.text = band.ToString() + " Score: " + brainWaveScore.ToString("F2");
    }
}
