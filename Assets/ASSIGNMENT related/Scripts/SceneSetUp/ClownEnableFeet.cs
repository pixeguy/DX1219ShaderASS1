using System.Collections;
using UnityEngine;

public class ClownEnableFeet : MonoBehaviour
{
    public GameObject leftFeet;
    public GameObject rightFeet;

    public void EnableLeft()
    {
        StartCoroutine(left());
    }

    public void EnableRight() 
    {
        StartCoroutine(right());
    }

    IEnumerator left()
    {
        leftFeet.SetActive(true);
        yield return new WaitForSeconds(0.1f);
        leftFeet.SetActive(false);
    }

    IEnumerator right()
    {
        rightFeet.SetActive(true);
        yield return new WaitForSeconds(0.1f);
        rightFeet.SetActive(false);
    }
}
