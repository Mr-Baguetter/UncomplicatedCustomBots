using System;
using System.Collections.Generic;
using MEC;
using UnityEngine;
using UnityEngine.AI;
using UncomplicatedCustomBots.API.Managers;

namespace UncomplicatedCustomBots.API.Features.NavigationSystem
{
    public static class PathPlanner
    {
        private const int DefaultMaxConcurrent = 2;
        private static int _running = 0;
        private static readonly Queue<Request> _queue = new();

        private sealed class Request
        {
            public Vector3 From;
            public Vector3 To;
            public int AreaMask;
            public Action<NavMeshPath?> OnComplete = null!;
            public int Retries = 0;
        }

        private static int GetMaxConcurrent()
        {
            try
            {
                int cfg = Plugin.Instance?.Config?.PathQueueConcurrency ?? DefaultMaxConcurrent;
                return Mathf.Clamp(cfg, 1, 8);
            }
            catch
            {
                return DefaultMaxConcurrent;
            }
        }

        public static void Enqueue(Vector3 from, Vector3 to, int areaMask, Action<NavMeshPath?> onComplete)
        {
            if (!NavMeshManager.IsBaked)
            {
                onComplete(null);
                return;
            }

            Request req = new()
            {
                From = from,
                To = to,
                AreaMask = areaMask,
                OnComplete = onComplete
            };

            if (_running < GetMaxConcurrent())
            {
                Timing.RunCoroutine(ProcessRoutine(req));
            }
            else
                _queue.Enqueue(req);
        }

        public static bool TryCalculateSync(Vector3 from, Vector3 to, int areaMask, out NavMeshPath path)
        {
            path = new NavMeshPath();
            if (!NavMeshManager.IsBaked)
                return false;

            if (!NavMesh.SamplePosition(from, out NavMeshHit sh, NavMeshManager.SampleMaxDistance, areaMask))
                return false;

            if (!NavMesh.SamplePosition(to, out NavMeshHit th, NavMeshManager.SampleMaxDistance, areaMask))
                return false;

            return NavMesh.CalculatePath(sh.position, th.position, areaMask, path);
        }

        private static IEnumerator<float> ProcessRoutine(Request req)
        {
            _running++;
            try
            {
                yield return Timing.WaitForOneFrame;

                NavMeshPath path = new();
                bool ok = false;

                if (NavMesh.SamplePosition(req.From, out NavMeshHit sh, NavMeshManager.SampleMaxDistance, req.AreaMask) && NavMesh.SamplePosition(req.To, out NavMeshHit th, NavMeshManager.SampleMaxDistance, req.AreaMask))
                {
                    ok = NavMesh.CalculatePath(sh.position, th.position, req.AreaMask, path);
                }
                else
                    ok = false;

                if (ok && path.status != NavMeshPathStatus.PathInvalid && path.corners != null && path.corners.Length > 0)
                {
                    req.OnComplete(path);
                }
                else
                    req.OnComplete(null);
            }
            finally
            {
                _running--;
                while (_queue.Count > 0 && _running < GetMaxConcurrent())
                {
                    Request next = _queue.Dequeue();
                    Timing.RunCoroutine(ProcessRoutine(next));
                    break;
                }
            }
        }

        public static float Jitter(float baseInterval) => baseInterval + UnityEngine.Random.Range(-2f, 2f);
    }
}
