using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(OSC))]
public class MuseListener : MonoBehaviour {

    public enum Band { Delta, Theta, Alpha, Beta, Gamma };

    #region BrainWave
    [System.Serializable]
    public class BrainWave{
        Band band;
        [SerializeField]
        float absoluteBandPower;
        public float AbsoluteBandPower { get { return absoluteBandPower; } set { absoluteBandPower = value; } }
        [SerializeField]
        float relativeBandPower;
        public float RelativeBandPower { get { return relativeBandPower; } set { relativeBandPower = value; } }
        [SerializeField, Range(0, 1)]
        float score;
        public float Score { get { return score; } }
        [HideInInspector]
        public AnimationCurve curve = new AnimationCurve();
        List<float> absoluteBandPowerDistribution;
        List<float> absoluteBandPowerHistory = new List<float>();
        List<float> absoluteBandPowerCurrent = new List<float>();
        int currentSize = 100;
        int historySize = 1000;
        float distributionCutoff = 0.2f;

        public BrainWave(Band _band){
            band = _band;
        }

        public void RecordAbsoluteBandPower(float f)
        {
            // update reference
            if (absoluteBandPowerHistory.Count < historySize)
                absoluteBandPowerHistory.Add(f);

            // update history
            absoluteBandPowerCurrent.Add(f);

            // update curve for Inspector visualization
            curve.AddKey(new Keyframe(absoluteBandPowerCurrent.Count, f));

            if (curve.length == currentSize){
                for (int i = 0; i < curve.length; i++)
                    curve.MoveKey(i, curve.keys[i + 1]);
            }

            if (absoluteBandPowerCurrent.Count == currentSize)
                absoluteBandPowerCurrent.RemoveAt(0);

            // sort history, create distribution
            absoluteBandPowerDistribution = new List<float>(absoluteBandPowerHistory);
            absoluteBandPowerDistribution.Sort();

            // calculate score
            float lowerLimit = Percentile(absoluteBandPowerDistribution, distributionCutoff);
            float upperLimit = Percentile(absoluteBandPowerDistribution, 1 - distributionCutoff);
            score = Mathf.Lerp(0, 1, (f - lowerLimit) / (upperLimit - lowerLimit));
        }

        public float Percentile(List<float> distribution, float percentile)
        {
            float realIndex = percentile * (distribution.Count - 1);
            int index = (int)realIndex;
            return distribution[index];
        }
    }
    #endregion

    #region Inspector Parameters
    // Headband Status
    [SerializeField]
    bool headbandStatus = false;
    public bool HeadbandStatus { get { return headbandStatus; } }

    [Header("Brain Waves")]
    [SerializeField]
    BrainWave delta = new BrainWave(Band.Delta);
    [SerializeField]
    BrainWave theta = new BrainWave(Band.Theta);
    [SerializeField]
    BrainWave alpha = new BrainWave(Band.Alpha);
    [SerializeField]
    BrainWave beta = new BrainWave(Band.Beta);
    [SerializeField]
    BrainWave gamma = new BrainWave(Band.Gamma);

    [Space]

    [SerializeField]
    Vector3 accelerometer;
    public Vector3 Accelerometer { get { return accelerometer; } }
    #endregion

    OSC osc;
    Dictionary<Band, BrainWave> brainWaveBandDictionary = new Dictionary<Band, BrainWave>();

    private void Awake()
    {
        // setup brain wave band dictionary
        brainWaveBandDictionary.Add(Band.Delta, delta);
        brainWaveBandDictionary.Add(Band.Theta, theta);
        brainWaveBandDictionary.Add(Band.Alpha, alpha);
        brainWaveBandDictionary.Add(Band.Beta, beta);
        brainWaveBandDictionary.Add(Band.Gamma, gamma);
    }

    void Start () {
        
        // check for OSC
        osc = GetComponent<OSC>();

        if (osc == null){
            Debug.LogError("No OSC Component found. MuseListener disabled.");
            gameObject.SetActive(false);
            return;
        }

        // accelerometer
        osc.SetAddressHandler("/muse/acc", AccelerometerListener);

        // Absolute Band Powers
        osc.SetAddressHandler("/muse/elements/delta_absolute", AbsoluteDeltaListener);
        osc.SetAddressHandler("/muse/elements/theta_absolute", AbsoluteThetaListener);
        osc.SetAddressHandler("/muse/elements/alpha_absolute", AbsoluteAlphaListener);
        osc.SetAddressHandler("/muse/elements/beta_absolute", AbsoluteBetaListener);
        osc.SetAddressHandler("/muse/elements/gamma_absolute", AbsoluteGammaListener);

        // Headband Status
        osc.SetAddressHandler("/muse/elements/touching_forehead", HeadbandStatusListener);
	}

    public BrainWave GetBrainWave(Band band)
    {
        if (brainWaveBandDictionary.ContainsKey(band))
            return brainWaveBandDictionary[band];
        return null;
    }

    float Average(float[] floats){
        float sum = 0;
        foreach (float f in floats) 
            sum += f;
        return sum / floats.Length;
    }

    void GetAbsoluteBandPower(OscMessage m, ref BrainWave brainWave){
        // get band powers
        float[] floats = {
            m.GetFloat(0),
            m.GetFloat(1),
            m.GetFloat(2),
            m.GetFloat(3)
        };

        // set band power
        float averageBandPower = Average(floats);
        brainWave.AbsoluteBandPower = averageBandPower;

        // record band power
        brainWave.RecordAbsoluteBandPower(averageBandPower);
    }

    #region OSC Message Listeners
    void HeadbandStatusListener(OscMessage m)
    {
        headbandStatus = m.GetInt(0) == 1;
    }

    void AbsoluteDeltaListener(OscMessage m)
    {
        GetAbsoluteBandPower(m, ref delta);
    }

    void AbsoluteThetaListener(OscMessage m)
    {
        GetAbsoluteBandPower(m, ref theta);
    }

    void AbsoluteAlphaListener(OscMessage m)
    {
        GetAbsoluteBandPower(m, ref alpha);
    }

    void AbsoluteBetaListener(OscMessage m)
    {
        GetAbsoluteBandPower(m, ref beta);
    }

    void AbsoluteGammaListener(OscMessage m)
    {
        GetAbsoluteBandPower(m, ref gamma);
    }

    void AccelerometerListener(OscMessage m)
    {
        accelerometer = new Vector3(
         m.GetFloat(0),
         m.GetFloat(1),
         m.GetFloat(2)
        );
    }
    #endregion

    #region Relative Band Powers
    void GetRelativeBandPowers(){
        CalculateRelativeBandPower(ref alpha);
        CalculateRelativeBandPower(ref beta);  
        CalculateRelativeBandPower(ref delta);  
        CalculateRelativeBandPower(ref gamma);  
        CalculateRelativeBandPower(ref theta);  
    }

    void CalculateRelativeBandPower(ref BrainWave brainWave){
        brainWave.RelativeBandPower = Mathf.Pow(10, brainWave.AbsoluteBandPower) / (Mathf.Pow(10, alpha.AbsoluteBandPower) + Mathf.Pow(10, beta.AbsoluteBandPower) + Mathf.Pow(10, delta.AbsoluteBandPower) + Mathf.Pow(10, gamma.AbsoluteBandPower) + Mathf.Pow(10, theta.AbsoluteBandPower));
    }
    #endregion

    void Update () {

        if(headbandStatus == true)
            GetRelativeBandPowers();
	}

    #if UNITY_EDITOR
    [CustomEditor(typeof(MuseListener))]
    public class MuseListenerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            MuseListener ml = (MuseListener)target;

            EditorGUILayout.LabelField("IP Address: ", IPManager.GetLocalIPAddress());

            float curveFieldHeight = 50;
            EditorGUILayout.CurveField("δ Delta", ml.delta.curve, Color.red, new Rect(), GUILayout.Height(curveFieldHeight));
            EditorGUILayout.CurveField("θ Theta", ml.theta.curve, Color.magenta, new Rect(), GUILayout.Height(curveFieldHeight));
            EditorGUILayout.CurveField("α Alpha", ml.alpha.curve, Color.cyan, new Rect(), GUILayout.Height(curveFieldHeight));
            EditorGUILayout.CurveField("β Beta", ml.beta.curve, Color.green, new Rect(), GUILayout.Height(curveFieldHeight));
            EditorGUILayout.CurveField("γ Gamma", ml.gamma.curve, Color.yellow, new Rect(), GUILayout.Height(curveFieldHeight));

            DrawDefaultInspector();
        }
    }
    #endif

    public static class IPManager
    {
        public static string GetLocalIPAddress()
        {
            var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }

            throw new System.Exception("No network adapters with an IPv4 address found.");
        }
    }
}
