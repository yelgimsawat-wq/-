using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace MagicDrawing
{
    public enum MatchState : byte
    {
        Waiting,    // ยังไม่ครบคน หรือยังไม่เริ่มรอบ
        Playing,    // กำลังสู้กันอยู่
        RoundOver,  // รู้ผลแล้ว รอเริ่มรอบใหม่
    }

    /// <summary>
    /// ตัวจัดการรอบการต่อสู้ ตัดสินแพ้ชนะและเริ่มรอบใหม่
    ///
    /// อยู่บน GameObject เดียวกับ NetworkManager จึงข้ามซีนไปด้วย
    /// และมีตัวเดียวในเกมเสมอ
    ///
    /// Server ตัดสินทุกอย่าง เครื่องผู้เล่นแค่อ่าน NetworkVariable ไปแสดงผล
    /// ถ้าปล่อยให้แต่ละเครื่องนับคนรอดเอง เน็ตหน่วงนิดเดียวก็ประกาศผู้ชนะ
    /// คนละคนแล้ว
    /// </summary>
    public class MatchManager : NetworkBehaviour
    {
        public static MatchManager Instance { get; private set; }

        [Tooltip("ต้องมีผู้เล่นอย่างน้อยกี่คนถึงจะเริ่มนับแพ้ชนะ")]
        [SerializeField] private int minPlayersToStart = 2;

        [Tooltip("จบรอบแล้วรอกี่วินาทีก่อนเริ่มรอบใหม่")]
        [SerializeField] private float restartDelay = 5f;

        [Tooltip("ข้อความประกาศผลกลางจอ ผูกโดยสคริปต์ติดตั้ง")]
        [SerializeField] private Text banner;

        private readonly NetworkVariable<MatchState> state = new NetworkVariable<MatchState>(
            MatchState.Waiting,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<int> aliveCount = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // ulong.MaxValue = ยังไม่มีผู้ชนะ ใช้ค่าพิเศษแทน nullable
        // เพราะ NetworkVariable รองรับเฉพาะชนิดที่มีขนาดคงที่
        private const ulong NoWinner = ulong.MaxValue;

        private readonly NetworkVariable<ulong> winnerClientId = new NetworkVariable<ulong>(
            NoWinner,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public MatchState State => state.Value;
        public int AliveCount => aliveCount.Value;

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// ต้องใส่ override และเรียก base ด้วย
        ///
        /// NetworkBehaviour มี OnDestroy ของตัวเองที่ทำงานสำคัญอยู่ คือถอนทะเบียน
        /// ตัวเองออกจากระบบเครือข่ายตอนถูกทำลาย ถ้าเขียนทับเฉย ๆ โดยไม่ใส่ override
        /// เมธอดของเราจะไปบังของเดิม งานถอนทะเบียนจึงไม่ได้ทำ แล้วจะมีของค้าง
        /// อยู่ในระบบจนขึ้น error ตอนเปลี่ยนซีนหรือออกจากห้อง
        /// </summary>
        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        /// <summary>
        /// ผู้เล่นคนหนึ่งตกรอบ เรียกจาก PlayerHealth ฝั่ง Server เท่านั้น
        /// </summary>
        public void ReportElimination(PlayerHealth victim)
        {
            if (!IsServer) return;

            RefreshAliveCount();

            // ยังไม่ได้เริ่มนับรอบ เช่นซ้อมคนเดียวหรือรอเพื่อนเข้ามา
            //
            // ตรงนี้เคยแค่ return เฉย ๆ ซึ่งทำให้คนที่ตายติดค้างอยู่ในสถานะตกรอบถาวร
            // เพราะการปลุกกลับมาเกิดตอนจบรอบเท่านั้น แต่รอบไม่มีวันจบเมื่อยังไม่ได้เริ่ม
            // ผลคือตายครั้งเดียวแล้วเล่นต่อไม่ได้เลย ต้องออกห้องแล้วเข้าใหม่
            if (state.Value != MatchState.Playing)
            {
                StartCoroutine(ReviveAfterDelay(victim));
                return;
            }

            List<PlayerHealth> alive = GetAlivePlayers();
            if (alive.Count > 1) return;

            ulong winner = alive.Count == 1 ? alive[0].OwnerClientId : NoWinner;
            EndRound(winner);
        }

        /// <summary>ผู้เล่นเข้ามาใหม่หรือเกิดใหม่ ให้ทบทวนว่าเริ่มรอบได้หรือยัง</summary>
        public void ReportSpawned()
        {
            if (!IsServer) return;

            RefreshAliveCount();

            if (state.Value == MatchState.Waiting && aliveCount.Value >= minPlayersToStart)
            {
                state.Value = MatchState.Playing;
                winnerClientId.Value = NoWinner;
            }
        }

        private void EndRound(ulong winner)
        {
            state.Value = MatchState.RoundOver;
            winnerClientId.Value = winner;

            StartCoroutine(RestartAfterDelay());
        }

        /// <summary>
        /// ปลุกคนที่ตายกลับมาเล่นต่อ ใช้ตอนที่ยังไม่ได้เริ่มนับรอบ
        /// จะได้ซ้อมยิงเวทคนเดียวได้โดยไม่ต้องออกห้องแล้วเข้าใหม่ทุกครั้งที่ตาย
        /// </summary>
        private IEnumerator ReviveAfterDelay(PlayerHealth victim)
        {
            yield return new WaitForSeconds(restartDelay);

            // ระหว่างรอ อาจมีเพื่อนเข้ามาจนรอบเริ่มไปแล้ว
            // ถ้าเป็นแบบนั้นให้ปล่อยไปตามกติกาปกติ ไม่ปลุกกลางรอบ
            if (state.Value == MatchState.Playing) yield break;

            if (victim != null) victim.ReviveServer();
            RefreshAliveCount();
        }

        private IEnumerator RestartAfterDelay()
        {
            yield return new WaitForSeconds(restartDelay);

            // ปลุกทุกคนกลับมาแล้วเริ่มใหม่
            foreach (PlayerHealth health in FindAllPlayers())
                health.ReviveServer();

            RefreshAliveCount();

            winnerClientId.Value = NoWinner;
            state.Value = aliveCount.Value >= minPlayersToStart
                ? MatchState.Playing
                : MatchState.Waiting;
        }

        private void RefreshAliveCount()
        {
            aliveCount.Value = GetAlivePlayers().Count;
        }

        private List<PlayerHealth> GetAlivePlayers()
        {
            var alive = new List<PlayerHealth>();

            foreach (PlayerHealth health in FindAllPlayers())
                if (!health.IsEliminated) alive.Add(health);

            return alive;
        }

        /// <summary>
        /// หาผู้เล่นทุกคนจากรายชื่อของ Netcode ไม่ใช่ FindObjectsOfType
        /// เพราะรายชื่อนี้เชื่อถือได้กว่าและไม่ต้องกวาดทั้งฉากทุกครั้ง
        /// </summary>
        private IEnumerable<PlayerHealth> FindAllPlayers()
        {
            if (NetworkManager == null) yield break;

            foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
            {
                if (client?.PlayerObject == null) continue;

                var health = client.PlayerObject.GetComponent<PlayerHealth>();
                if (health != null) yield return health;
            }
        }

        // ---------- แสดงผล ----------

        private void Update()
        {
            if (banner == null) return;

            banner.text = BuildBannerText();
            banner.gameObject.SetActive(!string.IsNullOrEmpty(banner.text));
        }

        private string BuildBannerText()
        {
            if (!IsSpawned) return "";

            switch (state.Value)
            {
                case MatchState.Waiting:
                    return aliveCount.Value < minPlayersToStart
                        ? $"รอผู้เล่นอีก {minPlayersToStart - aliveCount.Value} คน"
                        : "";

                case MatchState.RoundOver:
                    if (winnerClientId.Value == NoWinner) return "เสมอ — ไม่มีใครรอด";

                    bool youWon = NetworkManager != null
                                  && winnerClientId.Value == NetworkManager.LocalClientId;

                    return youWon ? "คุณชนะ!" : "คุณแพ้";

                default:
                    // ตกรอบแล้วต้องบอกให้ชัด ไม่งั้นผู้เล่นจะงงว่าทำไมคุมอะไรไม่ได้
                    // และต้องบอกปุ่มสลับมุมมองด้วย ไม่มีทางเดาเองได้
                    if (CameraFollow2D.IsSpectating)
                        return $"คุณตกรอบแล้ว — กด Tab เปลี่ยนคนที่ดู   (เหลือ {aliveCount.Value} คน)";

                    // ระหว่างสู้ บอกแค่จำนวนคนที่ยังรอด ไม่ต้องมีข้อความบังจอ
                    return aliveCount.Value > 1 ? $"เหลือ {aliveCount.Value} คน" : "";
            }
        }
    }
}
