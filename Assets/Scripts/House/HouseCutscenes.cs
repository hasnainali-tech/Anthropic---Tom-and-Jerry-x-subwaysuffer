using System;
using System.Collections;
using UnityEngine;

// In-engine story scenes (skippable): letterboxed camera moves with the real characters,
// short handwritten captions. Intro: the family leaves -> the cat wakes -> the mouse's den
// and empty pantry -> the mouse steps out. Ending: through the fence into the meadow.
public class HouseCutscenes : MonoBehaviour
{
    HouseGame game;
    HouseBuilder house;
    Camera cam;
    bool skip;
    Coroutine running;

    public void PlayIntro(HouseGame g, Action done)
    {
        game = g; house = g.house; cam = Camera.main; skip = false;
        running = StartCoroutine(Intro(done));
        HouseUI.Cutscene(true, () => skip = true);
    }

    public void PlayEnding(HouseGame g, Action done)
    {
        game = g; house = g.house; cam = Camera.main; skip = false;
        running = StartCoroutine(Ending(done));
        HouseUI.Cutscene(true, () => skip = true);
    }

    IEnumerator Intro(Action done)
    {
        var cat = game.cat;
        var catBody = cat.GetComponent<CatChaser>();
        game.mouse.InputEnabled = false;

        // ---- shot 1: the family leaves (suitcases out of the front door)
        HouseUI.Caption("Summer holidays! The family packed their bags for two whole weeks...");
        var door = house.FrontDoor;
        yield return Shot(house.FrontDoorShot, 0.9f, 5.2f, t =>
        {
            if (t > 0.15f && door && !door.open) { door.locked = false; door.SetOpen(true, quiet: true); AudioManager.PlayWhoosh(); }
            for (int i = 0; i < house.Suitcases.Count; i++)
            {
                var s = house.Suitcases[i];
                float k = Mathf.Clamp01((t - 0.3f - i * 0.12f) / 0.35f);
                if (!s) continue;
                s.position = Vector3.Lerp(SuitcaseStart(i), new Vector3(5f, 0f, -3.5f), k * k);
                s.gameObject.SetActive(k < 0.98f);
            }
            if (t > 0.86f && door && door.open) { door.SetOpen(false, quiet: true); door.locked = true; AudioManager.PlayClick(); }
        });
        if (skip) { FinishIntroState(); done(); yield break; }

        // ---- shot 2: the cat wakes up and stretches
        HouseUI.Caption("...and left the cat in charge of the house.");
        yield return Shot(house.CatShot, 0.6f, 4.6f, t =>
        {
            catBody.pose = t < 0.3f ? CatChaser.CatPose.Sleep : t < 0.65f ? CatChaser.CatPose.Stretch : CatChaser.CatPose.Search;
        });
        if (skip) { FinishIntroState(); done(); yield break; }

        // ---- shot 3: the den and the empty pantry
        HouseUI.Caption("Behind the wall lives a little mouse... with an empty pantry.");
        game.mouse.Teleport(house.PantryBox.transform.position + new Vector3(-0.3f, 0f, -1.2f), 60f);
        yield return Pan(house.DenShot, house.PantryShot, 5f);
        if (skip) { FinishIntroState(); done(); yield break; }

        // ---- shot 4: the mouse steps out of its hole
        HouseUI.Caption("Fill the pantry. Stay out of sight. Find the way to the garden.");
        game.mouse.Teleport(new Vector3(20.5f, 0.05f, -1.6f), 0f);
        game.mouse.ScriptedTarget = new Vector3(20.5f, 0f, 3.2f);
        game.mouse.scriptedSpeed = 2.6f;
        yield return Shot(house.HoleShot, 0.4f, 4.6f, null);
        FinishIntroState();
        done();
    }

    Vector3 SuitcaseStart(int i) => i == 0 ? new Vector3(4.2f, 0f, 2.6f) : new Vector3(6.6f, 0f, 2.4f);

    void FinishIntroState()
    {
        foreach (var s in house.Suitcases) if (s) s.gameObject.SetActive(false);
        if (house.FrontDoor) { house.FrontDoor.SetOpen(false, quiet: true); house.FrontDoor.locked = true; }
        var body = game.cat.GetComponent<CatChaser>();
        if (body) body.pose = CatChaser.CatPose.Auto;
        game.mouse.ScriptedTarget = null;
        if (house.RoomAt(game.mouse.transform.position) != "Living Room")
            game.mouse.Teleport(new Vector3(20.5f, 0.05f, 3.2f), 0f);   // just outside the hole
        HouseUI.Cutscene(false, null);
    }

    IEnumerator Ending(Action done)
    {
        var m = game.mouse;
        HouseUI.Caption("Through the gap in the fence...");
        m.ScriptedTarget = new Vector3(88f, 0f, 16f);
        m.scriptedSpeed = 4f;
        var shot = new GameObject("EndShot").transform;
        shot.position = new Vector3(84f, 1.6f, 9f);
        shot.LookAt(new Vector3(82f, 0.8f, 15.5f));
        yield return Shot(shot, 0.5f, 3.4f, t => { if (!skip) shot.LookAt(m.transform.position + Vector3.up * 0.8f); });
        HouseUI.Caption("Free! A whole meadow of seeds, berries and sunshine.");
        GameFX.Confetti(m.transform.position + Vector3.up * 2f);
        Vector3 from = cam.transform.position;
        float t2 = 0f;
        while (t2 < 3.4f && !skip)
        {
            t2 += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t2 / 3.4f);
            cam.transform.position = Vector3.Lerp(from, m.transform.position + new Vector3(-4f, 9f, -7f), k);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(m.transform.position + new Vector3(4f, 3f, 6f) - cam.transform.position), 2f * Time.deltaTime);
            yield return null;
        }
        Destroy(shot.gameObject);
        m.ScriptedTarget = null;
        m.IsWaving = true;
        HouseUI.Cutscene(false, null);
        done();
    }

    // camera holds a marker with a slow push-in; onTick gets 0..1 progress
    IEnumerator Shot(Transform mark, float push, float seconds, Action<float> onTick)
    {
        float t = 0f;
        Vector3 start = mark.position - mark.forward * push, end = mark.position;
        cam.fieldOfView = 55f;
        while (t < seconds)
        {
            if (skip) yield break;
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            cam.transform.SetPositionAndRotation(Vector3.Lerp(start, end, k), mark.rotation);
            onTick?.Invoke(t / seconds);
            yield return null;
        }
    }

    IEnumerator Pan(Transform a, Transform b, float seconds)
    {
        float t = 0f;
        cam.fieldOfView = 58f;
        while (t < seconds)
        {
            if (skip) yield break;
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            cam.transform.SetPositionAndRotation(Vector3.Lerp(a.position, b.position, k), Quaternion.Slerp(a.rotation, b.rotation, k));
            yield return null;
        }
    }
}
