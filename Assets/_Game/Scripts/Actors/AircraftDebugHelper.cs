using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AircraftDebugHelper : MonoBehaviour
{
    [Header("Route Position - Last Found")]
    [SerializeField] private Vector2 m_segmentVertex;
    [SerializeField] private int m_segmentVertexIndex;
    [SerializeField] private int m_segmentIndex;

    [HideInInspector] public Vector2 SegmentVertex { 
        set { 
            if (!value.Equals(m_segmentVertex))
                m_segmentVertex = value;
        } 
    }
    [HideInInspector] public int SegmentVertexIndex
    {
        set
        {
            if (!value.Equals(m_segmentVertexIndex))
            {
#if UNITY_EDITOR
                if (value < 0)
                    return; //Debug.LogError(string.Format("--------------- m_segmentVertexIndex lesser than 0: given value ", value));
                if (value > m_segmentVertexIndex && value - m_segmentVertexIndex > 1)
                    return;// Debug.LogWarning(string.Format("--------------- m_segmentVertexIndex incremented from {0} to {1}", m_segmentVertexIndex, value));
#endif
                m_segmentVertexIndex = value;
            }
        }
    }
    [HideInInspector] public int SegmentIndex
    {
        set
        {
            if (!value.Equals(m_segmentIndex))
            {
#if UNITY_EDITOR
                if (value < 0)
                    Debug.LogError(string.Format("--------------- m_segmentIndex lesser than 0: given value {0}", value));
#endif
                m_segmentIndex = value;
            }
        }
    }
}
