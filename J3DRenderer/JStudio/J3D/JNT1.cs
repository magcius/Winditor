﻿using GameFormatReader.Common;
using OpenTK;
using System.Collections.Generic;
using System.Diagnostics;
using WindEditor;

namespace JStudio.J3D
{
    public class SkeletonJoint
    {
        public string Name { get; internal set; }
        public ushort Unknown1 { get; internal set; }
        public ushort Unknown2 { get; internal set; }
        public Vector3 Scale { get; internal set; }
        public Quaternion Rotation { get; internal set; }
        public Vector3 Translation { get; internal set; }
        public float BoundingSphereDiameter { get; internal set; }
        public AABox BoundingBox { get; internal set; }

        // Useful for easier traversal
        public SkeletonJoint Parent { get; internal set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public class JNT1
    {
        public List<short> JointRemapTable;
        public List<SkeletonJoint> Joints;

        public void LoadJNT1FromStream(EndianBinaryReader reader, long tagStart)
        {
            ushort numJoints = reader.ReadUInt16();
            Trace.Assert(reader.ReadUInt16() == 0xFFFF); // Padding

            int jointDataOffset = reader.ReadInt32();
            int jointRemapDataOffset = reader.ReadInt32();
            int stringTableOffset = reader.ReadInt32();

            // Joint Names
            reader.BaseStream.Position = tagStart + stringTableOffset;
            StringTable nameTable = StringTable.FromStream(reader);

            // Joint Index Remap
            reader.BaseStream.Position = tagStart + jointRemapDataOffset;
            JointRemapTable = new List<short>();
            for (int i = 0; i < numJoints; i++)
                JointRemapTable.Add(reader.ReadInt16());

            // Joint Data
            reader.BaseStream.Position = tagStart + jointDataOffset;
            Joints = new List<SkeletonJoint>();
            for(int j = 0; j < numJoints; j++)
            {
                SkeletonJoint joint = new SkeletonJoint();
                Joints.Add(joint);

                joint.Name = nameTable[j];
                joint.Unknown1 = reader.ReadUInt16();
                joint.Unknown2 = reader.ReadUInt16();
                joint.Scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                Vector3 eulerRot = new Vector3();
                for (int e = 0; e < 3; e++)
                    eulerRot[e] = WMath.RotationShortToFloat(reader.ReadInt16());

                // ZYX order
                joint.Rotation = Quaternion.FromAxisAngle(new Vector3(0, 0, 1), WMath.DegreesToRadians(eulerRot.Z)) * 
                                 Quaternion.FromAxisAngle(new Vector3(0, 1, 0), WMath.DegreesToRadians(eulerRot.Y)) * 
                                 Quaternion.FromAxisAngle(new Vector3(1, 0, 0), WMath.DegreesToRadians(eulerRot.X));

                Trace.Assert(reader.ReadUInt16() == 0xFFFF); // Padding
                joint.Translation = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                joint.BoundingSphereDiameter = reader.ReadSingle();
                joint.BoundingBox = new AABox(new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()), new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
            }
        }

        public void CalculateParentJointsForSkeleton(HierarchyNode hierarchyRoot)
        {
            List<SkeletonJoint> processedJoints = new List<SkeletonJoint>();
            IterateHierarchyForSkeletonRecursive(hierarchyRoot, processedJoints, -1);
        }

        private void IterateHierarchyForSkeletonRecursive(HierarchyNode curNode, List<SkeletonJoint> processedJoints, int parentIndex)
        {
            switch (curNode.Type)
            {
                case HierarchyDataType.NewNode: parentIndex = processedJoints.Count - 1; break;
                case HierarchyDataType.Joint:
                    SkeletonJoint joint = Joints[JointRemapTable[curNode.Value]];
                    if(parentIndex >= 0)
                        joint.Parent = processedJoints[parentIndex];
                    processedJoints.Add(joint);
                    break;
            }

            foreach (var child in curNode.Children)
                IterateHierarchyForSkeletonRecursive(child, processedJoints, parentIndex);
        }
    }
}
