using UnityEngine;

namespace Sample
{
    /// <summary>
    /// 簡易的なエネミー生成
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        private GameObject[] _enemies;

        //生成の間隔（1秒）を決めるための変数
        private float _timeElapsed;
        public float _interval = 1f;
        public int _count = 0;
        public int _countMax = 100;

        public void Initialize(GameObject[] enemies)
        {
            _enemies = enemies;
        }

        private void Start()
        {
            _countMax = 20;
            _interval = 1f;

            _enemies = Resources.LoadAll<GameObject>("Enemy");
        }

        private void Update()
        {
            // Memo: StateMachine化する？
            if (_count < _countMax) Spawn();
        }

        private void Spawn()
        {
            //timeElapsedに経過時間(Time.deltaTime)を加算
            _timeElapsed += Time.deltaTime;
            if (_timeElapsed >= _interval)
            {
                Vector3 createPos = new Vector3(Random.Range(-40.0f, 40.0f), Random.Range(10.0f, 15.0f), Random.Range(-40.0f, 40.0f));

                if (Physics.SphereCast(createPos, 1f, Vector3.down, out RaycastHit hit, 15f))
                {
                    //Debug.Log(hit.collider.tag);
                    if (hit.collider.tag == "Ground")
                    {
                        //InstantiateでGameObject生成、Vector3(複製するGameObject,位置,回転)の順番で記載
                        //Instantiate(Enemy[Random.Range(0,Enemy.Length)], createPos, Quaternion.identity);
                        Instantiate(_enemies[Random.Range(0, _enemies.Length)], createPos, Quaternion.Euler(0f, Random.Range(0, 180), 0f));
                        _timeElapsed = 0.0f;
                        _count++;
                    }
                }
            }
        }
    }
}