using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ChangeColorModel : MonoBehaviour
{

    public MeshRenderer meshModel;
    // Start is called before the first frame update
    public void ChangeColor()
    {
        meshModel.material.color = EventSystem.current.currentSelectedGameObject.GetComponent<Image>().color;
    }
}
