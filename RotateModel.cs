using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RotateModel : MonoBehaviour
{

    public Slider slider;
    public GameObject model;

    // Start is called before the first frame update
    public void SliderRotatingModel()
    {
        model.transform.rotation = Quaternion.Euler(model.transform.rotation.x,slider.value,model.transform.rotation.z);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
