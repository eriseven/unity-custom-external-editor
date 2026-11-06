using UnityEngine;

public sealed class IntegrationFixture : MonoBehaviour
{
    public int FrameCount;

    private void Start()
    {
        var answer = 42;
        Debug.Log("ISOLATED_RIDER_RUNTIME answer=" + answer);
    }

    private void Update()
    {
        FrameCount++;
    }
}
