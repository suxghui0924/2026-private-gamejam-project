using UnityEngine;

namespace _Scripts.LSO.Data
{
    public class LSO_Ore : MonoBehaviour, LSO_IMinerable
    {
        public LSO_OreSO oreSO;

        private MeshRenderer _meshRenderer;

        private void Awake()
        {
            // 메시가 자식 오브젝트에 있는 경우까지 대응한다.
            _meshRenderer = GetComponentInChildren<MeshRenderer>();
        }

        private void Start()
        {
            Init();
        }

        private void Init()
        {
            if (oreSO == null)
            {
                Debug.LogError($"[LSO_Ore] '{name}' 에 OreSO가 지정되지 않았습니다.", this);
                return;
            }

            ApplyMaterial();
        }

        private void ApplyMaterial()
        {
            if (_meshRenderer == null)
            {
                Debug.LogWarning($"[LSO_Ore] '{name}' 및 자식에서 MeshRenderer를 찾지 못했습니다.", this);
                return;
            }

            if (oreSO.oreMaterial == null) return;

            // materials / sharedMaterials 는 배열의 '복사본'을 반환한다.
            // 반환값의 원소를 직접 바꾸면 그 복사본만 바뀌고 버려진다.
            // 반드시 지역 변수로 받아 수정한 뒤 다시 대입해야 한다.
            Material[] mats = _meshRenderer.sharedMaterials;
            if (mats.Length == 0) return;

            mats[0] = oreSO.oreMaterial;
            _meshRenderer.sharedMaterials = mats;
        }

        [ContextMenu("Mine")]
        public LSO_MineralSO Mine()
        {
            if (oreSO == null || oreSO.mineral == null)
            {
                Debug.LogWarning($"[LSO_Ore] '{name}' 에서 채굴할 광물 정보가 없습니다.", this);
                return null;
            }

            LSO_MineralSO mineral = oreSO.mineral;
            Debug.Log($"{mineral.mineralType}을(를) 채굴하여 {mineral.mineralPrice}를 얻었습니다!", this);

            return mineral;
        }

#if UNITY_EDITOR
        /// <summary>플레이하지 않고도 SO에 지정된 머티리얼을 확인한다.</summary>
        [ContextMenu("머티리얼 적용 미리보기")]
        private void PreviewMaterial()
        {
            if (oreSO == null) return;

            _meshRenderer = GetComponentInChildren<MeshRenderer>();
            ApplyMaterial();
        }
#endif
    }
}