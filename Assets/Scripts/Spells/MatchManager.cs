using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace MagicDrawing
{
    public enum MatchState : byte
    {
        Waiting,    // ยังไม่ครบคน หรือยังไม่เริ่มรอบ
        Playing,    // กำลังสู้กันอยู่
        RoundOver,  // รู้ผลแล้ว รอเริ่มรอบใหม่
    }

    /// <summary>
    /// ตัวจัดการรอบการต่อสู้ ตัดสินแพ้ชนะ
    ///
    /// ต้องถูก spawn จาก prefab ที่มี NetworkObject เท่านั้น
    ///
    /// เคยวางไว้บน GameObject เดียวกับ NetworkManager ซึ่งใช้ไม่ได้เลย
    /// เพราะ NetworkManager ห้ามมี NetworkObject อยู่ด้วย ตัวนี้จึงไม่เคย spawn
    /// IsServer เป็น false ตลอด แล้ว ReportElimination ก็ return ทิ้งทุกครั้ง
    /// ผลคือยิงจนเลือดหมดก็ไม่มีใครชนะ โดยไม่มี error ให้เห็นสักบรรทัด
    ///
    /// ตอนนี้ PlayerSpawner เป็นคน spawn ให้ตอนเข้าสนามรบ
    ///
    /// Server ตัดสินทุกอย่าง เครื่องผู้เล่นแค่อ่าน NetworkVariable ไปแสดงผล
    /// ถ้าปล่อยให้แต่ละเครื่องนับคนรอดเอง เน็ตหน่วงนิดเดียวก็ประกาศผู้ชนะ
    /// คนละคนแล้ว
    /// </summary>
    public class MatchManager : NetworkBehaviour
    {
        public static MatchManager Instance { get; private set; }

        [Tooltip("ต้องมีผู้เล่นอย่างน้อยกี่คนถึงจะเริ่มนับแพ้ชนะ "
                 + "ตั้ง 1 = เข้าเกมแล้วเล่นได้เลยไม่ต้องรอเพื่อน "
                 + "ตั้ง 2 = รอให้ครบสองคนก่อนถึงจะเริ่มนับ")]
        [SerializeField] private int minPlayersToStart = 1;

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

        /// <summary>
        /// ทะเบียนผู้เล่นที่อยู่ในรอบนี้
        ///
        /// เก็บเองแทนการไปไล่อ่าน NetworkManager.ConnectedClientsList เพราะตอนที่
        /// OnNetworkSpawn ของผู้เล่นทำงาน Netcode ยังไม่ทันผูก PlayerObject
        /// ให้ client คนนั้น ตัวที่กำลังเกิดจึงนับไม่ติด จำนวนคนจะช้ากว่าความจริง
        /// หนึ่งคนเสมอ มีสองคนก็นับได้หนึ่ง แล้วรอบไม่ยอมเริ่ม
        /// </summary>
        private readonly List<PlayerHealth> players = new List<PlayerHealth>();

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

            List<PlayerHealth> alive = GetAlivePlayers();

            // ยังไม่ได้เริ่มนับรอบ เช่นซ้อมคนเดียวหรือรอเพื่อนเข้ามา
            //
            // ปกติไม่ต้องตัดสินอะไร แต่ถ้าไม่เหลือใครเลยต้องปิดรอบให้จบ
            // ไม่งั้นคนที่ตายจะติดค้างในสถานะตกรอบถาวร เพราะการปลุกกลับมา
            // เกิดตอนจบรอบเท่านั้น แต่รอบจะจบได้ก็ต้องเริ่มก่อน
            // และรอบเริ่มได้ก็ต้องมีคนครบ เล่นคนเดียวจึงวนไม่ออก
            //
            // ไม่ปลุกกลับกลางคัน เพราะกติกาคือตายแล้วดูได้อย่างเดียว
            // ต้องรอจบรอบแล้วเริ่มใหม่พร้อมกันทุกคน
            if (state.Value != MatchState.Playing)
            {
                if (alive.Count == 0) EndRound(NoWinner);
                return;
            }

            if (alive.Count > 1) return;

            ulong winner = alive.Count == 1 ? alive[0].OwnerClientId : NoWinner;
            EndRound(winner);
        }

        /// <summary>ผู้เล่นเข้ามาใหม่ ลงทะเบียนแล้วทบทวนว่าเริ่มรอบได้หรือยัง</summary>
        public void ReportSpawned(PlayerHealth player)
        {
            if (!IsServer) return;

            if (player != null && !players.Contains(player)) players.Add(player);

            RefreshAliveCount();

            if (state.Value == MatchState.Waiting && aliveCount.Value >= minPlayersToStart)
            {
                state.Value = MatchState.Playing;
                winnerClientId.Value = NoWinner;
            }
        }

        /// <summary>
        /// ปิดรอบแล้วค้างผลไว้ ไม่เริ่มรอบใหม่เองและไม่ปลุกใครกลับมา
        ///
        /// กติกาของเกมนี้คือตายแล้วจบ ไม่มีเกิดใหม่ ผู้เล่นดูผลแล้วกดออกจากห้องเอง
        /// ถ้าเริ่มรอบใหม่อัตโนมัติ คนที่เพิ่งแพ้จะไม่ทันได้เห็นว่าใครชนะ
        /// </summary>
        private void EndRound(ulong winner)
        {
            state.Value = MatchState.RoundOver;
            winnerClientId.Value = winner;
        }

        /// <summary>ผู้เล่นออกจากเกม ถอนออกจากทะเบียนแล้วทบทวนผล</summary>
        public void ReportLeft(PlayerHealth player)
        {
            if (!IsServer) return;

            players.Remove(player);
            RefreshAliveCount();

            // คนออกกลางรอบก็นับเป็นเหลือคนเดียวได้ ต้องตัดสินด้วย
            // ไม่งั้นคนสุดท้ายจะยืนรอผลที่ไม่มีวันมา
            if (state.Value != MatchState.Playing) return;

            List<PlayerHealth> alive = GetAlivePlayers();
            if (alive.Count > 1) return;

            EndRound(alive.Count == 1 ? alive[0].OwnerClientId : NoWinner);
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

        /// <summary>ผู้เล่นทุกคนที่ลงทะเบียนไว้ พร้อมเก็บกวาดตัวที่หายไปแล้ว</summary>
        private IEnumerable<PlayerHealth> FindAllPlayers()
        {
            // เดินถอยหลังเพื่อเก็บกวาดตัวที่ถูกทำลายไปแล้วได้ระหว่างวน
            // เช่นผู้เล่นหลุดออกไปแบบไม่ทันแจ้ง
            for (int i = players.Count - 1; i >= 0; i--)
            {
                if (players[i] == null)
                {
                    players.RemoveAt(i);
                    continue;
                }

                yield return players[i];
            }
        }

        // ---------- แสดงผล ----------

        private void Update()
        {
            // ถามหาป้ายตอนทำงานจริง ไม่ใช่ผูกไว้ล่วงหน้า
            // เพราะตัวนี้ถูกสร้างจาก prefab ซึ่งอ้างถึงของในฉากไม่ได้
            MatchBanner target = MatchBanner.Instance;
            if (target == null) return;

            target.Show(BuildBannerText());
        }

        private string BuildBannerText()
        {
            if (!IsSpawned) return "";

            switch (state.Value)
            {
                case MatchState.Waiting:
                    // ต้องบอกให้ครบว่ารออะไรและระหว่างรอทำอะไรได้
                    // ถ้าเขียนแค่ "รอผู้เล่นอีก 1 คน" ผู้เล่นจะงงว่าตัวเองเข้าเกมแล้ว
                    // ทำไมยังขึ้นว่ารออยู่ และไม่รู้ว่าเดินยิงซ้อมไปก่อนได้
                    return aliveCount.Value < minPlayersToStart
                        ? $"รอเพื่อนอีก {minPlayersToStart - aliveCount.Value} คนถึงจะเริ่มนับแพ้ชนะ"
                          + "   —   ระหว่างนี้ซ้อมวาดเวทได้ตามปกติ"
                        : "";

                case MatchState.RoundOver:
                    // ต้องบอกทางออกด้วย เพราะเกมไม่เริ่มรอบใหม่เอง
                    // ถ้าบอกแค่ผลแพ้ชนะ ผู้เล่นจะนั่งรอว่าเมื่อไรจะเริ่มใหม่
                    const string howToLeave = "   —   กดออกจากห้องเพื่อกลับหน้าเมนู";

                    if (winnerClientId.Value == NoWinner)
                        return "เสมอ — ไม่มีใครรอด" + howToLeave;

                    bool youWon = NetworkManager != null
                                  && winnerClientId.Value == NetworkManager.LocalClientId;

                    return (youWon ? "คุณชนะ!" : "คุณแพ้") + howToLeave;

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
