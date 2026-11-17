using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        // GetAxisRaw를 사용해 키 입력을 뗐을 때 미끄러지지 않고 즉시 멈추도록 처리
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        // 대각선 이동 시 속도가 빨라지는 현상 방지를 위해 normalized 적용
        moveInput = new Vector2(moveX, moveY).normalized;
    }

    private void FixedUpdate()
    {
        // 물리 연산 주기에 맞춰 이동 속도 적용 (Unity 6 미만 버전은 linearVelocity 대신 velocity 사용)
        rb.linearVelocity = moveInput * moveSpeed;
    }
}