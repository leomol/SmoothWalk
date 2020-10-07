/* 
 * 2015-09-19. Leonardo Molina.
 * 2019-08-05. Last modification.
 */

/*
	Read touch contacts and allow short interruptions (pointer lift, e.g. end/cancel) while maintaining the id of such gesture.
	
	Certain: Moved/Stationary
	Uncertain: Began/Ended/Canceled
	Valid: certain + passed uncertain.
	
	When a touch ends and another starts (both unreliable states) within a predicted region, assume it's the same touch.
	The prediction region is a circular sector (a pizza slice) starting at the last (presumably-valid) point.
	Validity test:
		Does the test-point lie within a threshold radius from the reference-point?
			Is the angle between the forward-axis and a line from reference-point to the test-point smaller than an angle threshold?
				Assume valid.
	
	When the validity test fails, assume the end and start phases are valid for those touch events.
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pointers;

public class PointerDebounce : MonoBehaviour {
	PointerGetter pointerGetter;
	
	// Max duration of an interruption, i.e., how long to wait for a touch to recover.
	float duration;
	// Max length in terms of length of expected trace that a trace may have in order to qualify for score testing.
	float radius;
	// Speed-dependent circular sector where the tested trace must lie in order to qualify for score testing. (p degrees when speed is q, in pixel per s).
	float slow;		// Slow speed at which separation is wide.
	float wide;
	float fast;		// Fast speed at which separation is narrow.
	float narrow;
	float sliceSlope;
	float sliceOffset;
	// A full circular sector is enforced when distance is under a threshold.
	float resolution;
	
	// Pointers to report.
	List<Pointer> reportPointers = new List<Pointer>(20);
	// Potential continuity.
	List<Pointer> began = new List<Pointer>(20);
	List<Pointer> ended = new List<Pointer>(20);
	
	// Map touch.fingerId of a recent trace to an older trace considered continuation.
	Dictionary<int, int> links = new Dictionary<int, int>();
	// Map unity fingerId to a unique id.
	Dictionary<int, int> unityToUnique = new Dictionary<int, int>();
	List<int> uniqueIds = new List<int>();
	
	// Return an id 
	int GetUniqueId(int unityId) {
		if (!unityToUnique.ContainsKey(unityId)) {
			int uniqueId = 0;
			while (uniqueIds.Contains(uniqueId))
				uniqueId += 1;
			uniqueIds.Add(uniqueId);
			unityToUnique[unityId] = uniqueId;
		}
		return unityToUnique[unityId];
	}
	
	void ClearUniqueId(int unityId) {
		unityToUnique.Remove(unityId);
	}
	
	int GetLink(int uniqueId) {
		int linkedId = uniqueId;
		if (links.ContainsKey(uniqueId))
			linkedId = links[uniqueId];
		return linkedId;
	}
	
	void RemoveLinkTo(int p2) {
		// Remove link if existed.
		foreach (int p in links.Keys) {
			if (links[p] == p2) {
				links.Remove(p);
				break;
			}
		}
	}
	
	int pointerCount = 0;
	
	public int PointerCount {
		get {
			return pointerCount;
		}
	}
	
	public List<Pointer> Pointers {
		get {
			return reportPointers;
		}
	}
	
	// Parameters to create a speed-dependent circular sector.
	// This circular sector (similar to a pizza slice) is designed to become longer and narrower with greater speeds.
	// At 0 cm/s such slice covers 315 deg; at 20cm/s or higher, it covers 45 deg.
	public void Setup(float duration, float resolution, float radius, float slow, float wide, float fast, float narrow) {
		if (slow > fast) {
			float tmp = slow;
			slow = fast;
			fast = tmp;
		}
		if (narrow > wide) {
			float tmp = narrow;
			narrow = wide;
			wide = tmp;
		}
		this.duration = duration;
		this.resolution = resolution;
		this.radius = radius;
		this.narrow = narrow;
		this.wide = wide;
		this.sliceSlope = (narrow - wide) / (fast - slow);
		this.sliceOffset = wide - sliceSlope * slow;
	}
	
	void Awake() {
		pointerGetter = Components.Get("PointerGetter") as PointerGetter;
		float dpcm = Screen.dpi / 2.54f;
		Setup(0.020f, 0.5f * dpcm, 5f, 0.5f * 315f, 0.5f * 45f, 0f * dpcm, 20f * dpcm);
	}
	
	void Update() {
		Process(Time.time, pointerGetter.DragPointers);
	}
	
	string lastIdString = "";
	void Process(float tic,	List<Pointer> loosePointers) {
		pointerCount = 0;
		began = new List<Pointer>(20);
		reportPointers = new List<Pointer>(20);
		string unityIdsString = "";
		string uniqueIdsString = "";
		foreach (Pointer loosePointer in loosePointers) {
			int unityId = loosePointer.fingerId;
			int uniqueId = GetUniqueId(unityId);
			unityIdsString += unityId + " ";
			uniqueIdsString += uniqueId + " ";
			//int uniqueId = unityId;
			if (loosePointer.phase == TouchPhase.Began) {
				loosePointer.fingerId = uniqueId;
				// This pointer could potentially be connected to a nearby pointer that was recently in phase-ended.
				began.Add(loosePointer);
				pointerCount += 1;
			} else if (loosePointer.phase == TouchPhase.Ended || loosePointer.phase == TouchPhase.Canceled) {
				// fingerId has to be different than any test pointer (phase-ended).
				loosePointer.fingerId = uniqueId;
				// This phase is uncertain, hence, consider it is still moving, then later test if this pointer can be connected to a nearby pointer in phase-began.
				loosePointer.phase = TouchPhase.Moved;
				ended.Add(loosePointer);
				// Do not map this id again.
				ClearUniqueId(unityId);
				pointerCount += 1;
			} else if (loosePointer.phase == TouchPhase.Moved || loosePointer.phase == TouchPhase.Stationary) {
				// If this pointer is currently mapped to an older trace, rename.
				loosePointer.fingerId = GetLink(uniqueId);
				// These states are certainly valid.
				reportPointers.Add(loosePointer);
				pointerCount += 1;
			}
		}
		// string idString = string.Format("UnityId:{0}\nUniqueId:{1}", unityIdsString, uniqueIdsString);
		// if (idString != lastIdString) {
			// lastIdString = idString;
			// Debug.Log(idString); // !!
		// }
		
		/* Compare all phase-ended with this frame's phase-began.
			Naming convention:
				3: Touch phase = began.
				2: Touch phase = end.
				1: Touch phase = moving.
		*/
		List<Score> scores = new List<Score>();
		for (int p3 = 0; p3 < began.Count; p3++) {
			for (int p2 = 0; p2 < ended.Count; p2++) {
				// Check if trace length is smaller than the threshold length (speed dependent).
				// Current delta position (test trace) and delta time.
				// Test vector: line between position-ended (from an older frame) and position-began.
				Vector2 trace32 = began[p3].position - ended[p2].position;
				float traceLength32 = trace32.magnitude;
				float deltaTime32 = tic - ended[p2].time;
				Vector2 trace21 = ended[p2].deltaPosition;
				// Previous speed.
				float speed21 = trace21.magnitude / ended[p2].deltaTime;
				// Extrapolated trace length: previous speed x current dt.
				float expectedLength32 = speed21 * deltaTime32;
				// difference of trace length (expected trace length compared to trace length, normalized to 1")
				float distancePenalty = Mathf.Abs(traceLength32 - expectedLength32) / Screen.dpi;
				if (traceLength32 < resolution) {
					scores.Add(new Score(p3, p2, distancePenalty));
				} else {
					// Speed of test trace.
					float speed32 = traceLength32 / deltaTime32;
					// Threshold length: factor x current expected trace length.
					float thresholdLength32 = radius * expectedLength32;
					float separationPenalty = Vector2.Angle(trace32, trace21);
					// Debug.Log(string.Format("{0}..{1}: {2:0.00}", trace32.x, trace21[p2].x, separationPenalty));
					if (traceLength32 < thresholdLength32) {
						// Radius thresholding passed.
						// Check if current trace has an angular orientation within a slice centered with previous trace.
						float thresholdSeparation32to21 = Mathf.Min(Mathf.Max(sliceOffset + sliceSlope * speed32, narrow), wide);
						if (separationPenalty < thresholdSeparation32to21) {
							// Smaller separation and distance penalties score better (towards zero).
							float score = separationPenalty / thresholdSeparation32to21 + distancePenalty;
							scores.Add(new Score(p3, p2, score));
						}
					}
				}
			}
		}
		
		// Sort scores by value.
		scores.Sort((v1, v2) => v1.score.CompareTo(v2.score));
		// Keep pair of points that ranked best. Remove pair of points that shared either of such points.
		bool[] scoresIgnore = new bool[scores.Count];
		bool[] beganRemove = new bool[began.Count];
		bool[] endedRemove = new bool[ended.Count];
		for (int a = 0; a < scores.Count; a++) {
			beganRemove[scores[a].p3] = true;
			endedRemove[scores[a].p2] = true;
			for (int b = a + 1; b < scores.Count; b++)
				if (scores[b].p3 == scores[a].p3 || scores[b].p2 == scores[a].p2)
					scoresIgnore[b] = true;
		}
		
		// Connect lose ends.
		for (int s = 0; s < scores.Count; s++) {
			if (!scoresIgnore[s]) {
				pointerCount += 1;
				//count -= 1;
				int p3 = scores[s].p3;
				int p2 = scores[s].p2;
				int id3 = began[p3].fingerId;
				int id2 = ended[p2].fingerId;
				// New link from new id to old id.
				links[id3] = id2;
				Pointer linked = began[p3];
				linked.fingerId = id2;
				linked.phase = TouchPhase.Moved;
				linked.deltaTime = linked.time - ended[p2].time;
				linked.deltaPosition = linked.position - ended[p2].position;
				// Change touch-began from list-began to list-moved.
				reportPointers.Add(linked);
			}
		}
		
		// Add began pointers that were not cleared (linked pointers from list-began). Separate loop.
		for (int p3 = 0; p3 < began.Count; p3++) {
			if (!beganRemove[p3])
				reportPointers.Add(began[p3]);
		}
		
		// Clear expired pointers or pointers relocated to moved-list from list-ended. Separate loop.
		// pointer-end had already been copied to list-pointers with phase=moved during it's phase-end.
		List<Pointer> endedBuffer = new List<Pointer>(ended.Count);
		for (int p2 = 0; p2 < ended.Count; p2++) {
			if (!endedRemove[p2] && tic - ended[p2].time < duration) {
				endedBuffer.Add(ended[p2]);
			} else {
				// Remove links from this id.
				uniqueIds.Remove(ended[p2].fingerId);
				RemoveLinkTo(ended[p2].fingerId);
				// Change phase once again: Ended --> Moved --> Ended.
				ended[p2].phase = TouchPhase.Ended;
				// Add to report.
				reportPointers.Add(ended[p2]);
			}
		}
		ended = endedBuffer;
	}
}

class Score {
	public int p3;
	public int p2;
	public float score;
	public Score(int p3, int p2, float score) {
		this.p3 = p3;
		this.p2 = p2;
		this.score = score;
	}
}