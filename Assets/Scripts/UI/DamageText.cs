using TMPro;
using UnityEngine;

public class DamageText : MonoBehaviour
{
    [SerializeField] private TextMeshPro tmp; // World Space text, not TextMeshProUGUI

    private float _floatSpeed = 0.5f;
    private float _fadeDuration = 0.5f;
    private float _timer;
    private Camera _cam;

    public void Setup(int damage)
    {
        tmp.text = damage.ToString();
        _cam = Camera.main;
    }

    private void Update()
    {
        _timer += Time.deltaTime;

        // Float upward
        transform.position += Vector3.up * _floatSpeed * Time.deltaTime;

        // Always face the camera (billboard effect)
        transform.rotation = _cam.transform.rotation;

        // Fade out
        float alpha = Mathf.Lerp(1f, 0f, _timer / _fadeDuration);
        tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, alpha);

        if (_timer >= _fadeDuration)
            Destroy(gameObject);
    }
}