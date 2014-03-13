using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KMP
{
    public class KMPVessel
    {

        //Properties
        public KMPVesselInfo info;

        public String vesselName
        {
            private set;
            get;
        }

        public String ownerName
        {
            private set;
            get;
        }

        public Guid id
        {
            private set;
            get;
        }
        #region Private variables
        private Orbit referenceOrbit;
        private Quaternion referenceRotation;
        private Vector3d referenceSurfacePosition;
        private Vector3 referenceSurfaceVelocity;
        private Vector3 referenceAngularVelocity;
        private Vector3 referenceAcceleration;
        private Situation referenceVesselSituation;
        private double referenceUT;
        #endregion
        public Orbit currentOrbit
        {
            get
            {
                if (referenceOrbit == null)
                {
                    return null;
                }

                Orbit tempOrbit = new Orbit(referenceOrbit.inclination, referenceOrbit.eccentricity, referenceOrbit.semiMajorAxis, referenceOrbit.LAN, referenceOrbit.argumentOfPeriapsis, referenceOrbit.meanAnomalyAtEpoch, referenceOrbit.epoch, referenceOrbit.referenceBody);
                //Orbit doesn't have a copy constructor...
                tempOrbit.Init();

                double timeNow = Planetarium.GetUniversalTime();

                //Sync orbit backwards
                if (tempOrbit.StartUT > timeNow)
                {
                    KMP.Log.Debug("Reference orbit begins at " + tempOrbit.EndUT + ", updating to " + timeNow);

                    while (tempOrbit.StartUT > timeNow || tempOrbit == null)
                    {

                        if (tempOrbit != null ? tempOrbit.previousPatch != null : false)
                        {
                            KMP.Log.Debug("Updating orbit from " + tempOrbit.referenceBody.bodyName + "(begins " + tempOrbit.StartUT + ") to " + tempOrbit.previousPatch.referenceBody.bodyName + " (starts " + tempOrbit.previousPatch.StartUT + ")");
                        }
                        else
                        {
                            KMP.Log.Debug("Updating orbit from " + tempOrbit.referenceBody.bodyName + " to null (no next patch available)");
                        }
                        tempOrbit = tempOrbit.nextPatch;
                        tempOrbit.Init();
                    }
                }

                //Sync orbit forwards
                if ((tempOrbit.EndUT < timeNow) && (tempOrbit.EndUT != 0))
                {
                    KMP.Log.Debug("Reference orbit ends at " + tempOrbit.EndUT + ", updating to " + timeNow);
                    
                    while ((tempOrbit.EndUT < timeNow) && (tempOrbit.EndUT != 0) && tempOrbit != null)
                    {

                        if (tempOrbit != null ? tempOrbit.nextPatch != null : false)
                        {
                            KMP.Log.Debug("Updating orbit from " + tempOrbit.referenceBody.bodyName + "(ends " + tempOrbit.EndUT + ") to " + tempOrbit.nextPatch.referenceBody.bodyName + " (ends " + tempOrbit.nextPatch.EndUT + ")");
                        }
                        else
                        {
                            KMP.Log.Debug("Updating orbit from " + tempOrbit.referenceBody.bodyName + " to null (no next patch available)");
                        }
                        tempOrbit = tempOrbit.previousPatch;
                        tempOrbit.Init();
                    }
                }

                tempOrbit.UpdateFromUT(Planetarium.GetUniversalTime());

                //Update the orbit
                if (tempOrbit == null)
                {
                    KMP.Log.Debug("KMPVessel: New orbit is null!");
                }

                return tempOrbit;
            }
        }

        private bool useSurfacePositioning
        {
            get
            {
                return (situationIsGrounded(referenceVesselSituation) || referenceSurfacePosition.z < 10000);
            }
        }
        #region Surface positioning
        public Vector3d surfaceModePosition
        {
            get
            {
                if (orbitValid)
                {
                    //This uses the most awesome algorithm of position = position + (surface_velocity * time_difference).
                    //surfaceMotionPrediciton returns Vector3d.zero if we aren't in a situation to use it.
                    return currentOrbit.referenceBody.GetWorldSurfacePosition(referenceSurfacePosition.x, referenceSurfacePosition.y, (referenceSurfacePosition.z + 0.1)) + surfacePositionPrediction;
                }
                return Vector3d.zero;
            }
        }

        public Vector3d surfaceModeVelocity
        {
            get
            {
                if (orbitValid)
                {
                    //return vesselRef.rigidbody.transform.TransformDirection(referenceSurfaceVelocity);
                    return currentOrbit.referenceBody.transform.TransformDirection(referenceSurfaceVelocity) + velocityPrediction;
                }
                return Vector3d.zero;
            }

        }
        //Returns a vector in world co-ordinates of the estimated surface transform if we are well in-sync.
        public Vector3d surfacePositionPrediction
        {
            get
            {
                if (vesselRef != null ? vesselRef.rigidbody != null : false)
                {
                    Vector3d fudge = vesselRef.mainBody.transform.TransformDirection(referenceSurfaceVelocity);
                    float timeDelta = (float)(Planetarium.GetUniversalTime() - referenceUT);
                    //These values should probably be constants somewhere.
                    //Max prediction: 3 seconds.
                    //Has to be grounded as this is intended for rover drag racing.
                    if (Math.Abs(timeDelta) < 3)
                    {
                        return (fudge * timeDelta);
                    }
                }
                return Vector3d.zero;
            }
        }

        public Vector3d velocityPrediction
        {
            get
            {
                if (vesselRef != null ? vesselRef.rigidbody != null : false)
                {
                    Vector3d fudge = vesselRef.mainBody.transform.TransformDirection(referenceAcceleration);
                    float timeDelta = (float)(Planetarium.GetUniversalTime() - referenceUT);
                    //These values should probably be constants somewhere.
                    //Max prediction: 3 seconds.
                    if (Math.Abs(timeDelta) < 3)
                    {
                        return (fudge * timeDelta);
                    }
                }
                return Vector3d.zero;
            }
        }
        #endregion
        public GameObject gameObj
        {
            private set;
            get;
        }

        public LineRenderer line
        {
            private set;
            get;
        }

        public OrbitRenderer orbitRenderer
        {
            private set;
            get;
        }

        public Color activeColor
        {
            private set;
            get;
        }

        public bool orbitValid
        {
            get
            {
                if (currentOrbit == null)
                {
                    return false;
                }
                return true;
            }
        }

        public bool shouldShowOrbit
        {
            get
            {
                if (!orbitValid || situationIsGrounded(info.situation))
                {
                    return false;
                }
                else
                {
                    return info.state == State.ACTIVE || orbitRenderer.mouseOver;
                }
            }
        }

        public Vessel vesselRef;
        //Methods
        public KMPVessel(String vessel_name, String owner_name, Guid _id)
        {
            info = new KMPVesselInfo();

            vesselName = vessel_name;
            ownerName = owner_name;
            id = _id;

            //Build the name of the game object
            System.Text.StringBuilder sb = new StringBuilder();
            sb.Append(vesselName);
//			sb.Append(" (");
//			sb.Append(ownerName);
//			sb.Append(')');

            gameObj = new GameObject(sb.ToString());
            gameObj.layer = 9;

            generateActiveColor();

            line = gameObj.AddComponent<LineRenderer>();
            orbitRenderer = gameObj.AddComponent<OrbitRenderer>();
            orbitRenderer.driver = new OrbitDriver();
			
            line.transform.parent = gameObj.transform;
            line.transform.localPosition = Vector3.zero;
            line.transform.localEulerAngles = Vector3.zero;

            line.useWorldSpace = true;
            line.material = new Material(Shader.Find("Particles/Alpha Blended Premultiply"));
            line.SetVertexCount(2);
            line.enabled = false;

        }

        public void generateActiveColor()
        {
            //Generate a display color from the owner name
            activeColor = generateActiveColor(ownerName);
        }

        public static Color generateActiveColor(String str)
        {
            int val = 5381;

            foreach (char c in str)
            {
                val = ((val << 5) + val) + c;
            }

            return generateActiveColor(Math.Abs(val));
        }

        public static Color generateActiveColor(int val)
        {
            switch (val % 17)
            {
                case 0:
                    return Color.red;

                case 1:
                    return new Color(1, 0, 0.5f, 1); //Rosy pink
					
                case 2:
                    return new Color(0.6f, 0, 0.5f, 1); //OU Crimson
					
                case 3:
                    return new Color(1, 0.5f, 0, 1); //Orange
					
                case 4:
                    return Color.yellow;
					
                case 5:
                    return new Color(1, 0.84f, 0, 1); //Gold
					
                case 6:
                    return Color.green;
					
                case 7:
                    return new Color(0, 0.651f, 0.576f, 1); //Persian Green
					
                case 8:
                    return new Color(0, 0.651f, 0.576f, 1); //Persian Green
					
                case 9:
                    return new Color(0, 0.659f, 0.420f, 1); //Jade
					
                case 10:
                    return new Color(0.043f, 0.855f, 0.318f, 1); //Malachite
					
                case 11:
                    return Color.cyan;					

                case 12:
                    return new Color(0.537f, 0.812f, 0.883f, 1); //Baby blue;

                case 13:
                    return new Color(0, 0.529f, 0.741f, 1); //NCS blue
					
                case 14:
                    return new Color(0.255f, 0.412f, 0.882f, 1); //Royal Blue
					
                case 15:
                    return new Color(0.5f, 0, 1, 1); //Violet
					
                default:
                    return Color.magenta;
					
            }
        }
        #region Data setter
        public void setPositioningData(Orbit new_orbit, Quaternion rigidbody_rotation, Vector3d surface_position, Vector3 surface_velocity, Vector3 angular_velocity, Vector3 acceleration, Situation vessel_situation, double update_tick)
        {
            if (new_orbit == null)
            {
                KMP.Log.Debug("New orbit is null!");
                return;
            }
            referenceOrbit = new_orbit;
            referenceRotation = rigidbody_rotation;
            referenceSurfacePosition = surface_position;
            referenceSurfaceVelocity = surface_velocity;
            referenceAngularVelocity = angular_velocity;
            referenceAcceleration = acceleration;
            referenceVesselSituation = vessel_situation;
            referenceUT = update_tick;
            return;
        }
        #endregion
        //Thanks HyperEdit.
        private static void HardsetOrbit(Orbit orbit, Orbit newOrbit)
        {
            orbit.inclination = newOrbit.inclination;
            orbit.eccentricity = newOrbit.eccentricity;
            orbit.semiMajorAxis = newOrbit.semiMajorAxis;
            orbit.LAN = newOrbit.LAN;
            orbit.argumentOfPeriapsis = newOrbit.argumentOfPeriapsis;
            orbit.meanAnomalyAtEpoch = newOrbit.meanAnomalyAtEpoch;
            orbit.epoch = newOrbit.epoch;
            orbit.referenceBody = newOrbit.referenceBody;
            orbit.Init();
            orbit.UpdateFromUT(Planetarium.GetUniversalTime());
        }

        public void updatePosition(bool updateShip)
        {
            Orbit updateOrbit = currentOrbit;
            if (updateOrbit == null)
            {
                KMP.Log.Debug("updateOrbit is null!");
                return;
            }

            #region Where the magic happens
            //If the vessel exists and this is an updateShip call

            double distance = 3000f;
            bool allowUpdate = true;
            if (FlightGlobals.ActiveVessel != null)
            {
                if (HighLogic.LoadedScene != GameScenes.TRACKSTATION)
                {
                    distance = Vector3d.Distance(vesselRef.GetWorldPos3D(), FlightGlobals.ActiveVessel.GetWorldPos3D());
                }
            }

            //Give the vessel a change to pack and unpack
            if (vesselRef.packed && (distance < vesselRef.distanceUnpackThreshold))
            {
                allowUpdate = false;
            }
            if (!vesselRef.packed && (distance > vesselRef.distancePackThreshold))
            {
                allowUpdate = false;
            }

            //Set the positions.
            if ((vesselRef != null) && updateShip && allowUpdate)
            {
                //KMP.Log.Debug("Before distance: " + Vector3d.Distance(vesselRef.orbit.getTruePositionAtUT(Planetarium.GetUniversalTime()), vesselRef.GetWorldPos3D()));
                /* Thanks HyperEdit */

                if (useSurfacePositioning)
                {
                    if (!vesselRef.packed)
                    {
                        vesselRef.SetPosition(surfaceModePosition);
                        vesselRef.SetWorldVelocity(surfaceModeVelocity - Krakensbane.GetFrameVelocity());
                    }
                }
                else
                {
                    if (vesselRef.packed)
                    {
                        HardsetOrbit(vesselRef.orbitDriver.orbit, updateOrbit);
						vesselRef.orbitDriver.pos = vesselRef.orbit.pos.xzy;
						vesselRef.orbitDriver.vel = vesselRef.orbit.vel;
                    }
                    else
                    {
						vesselRef.SetPosition(updateOrbit.getTruePositionAtUT(Planetarium.GetUniversalTime()));
                        vesselRef.SetWorldVelocity(updateOrbit.vel.xzy - Krakensbane.GetFrameVelocity());
                    }
                }
                //KMP.Log.Debug("After distance: " + Vector3d.Distance(vesselRef.orbit.getTruePositionAtUT(Planetarium.GetUniversalTime()), vesselRef.GetWorldPos3D()));
                if (!vesselRef.packed)
                {
                    vesselRef.angularVelocity = Vector3.zero;
                    //vesselRef.angularVelocity = vesselRef.transform.InverseTransformDirection(referenceAngularVelocity);
                    vesselRef.SetRotation(referenceRotation);
                }
            }

            #endregion

            //If the gameObject exists and we are in the map or tracking station and it's not a ship update
            if (!updateShip && (referenceOrbit != null) && (gameObj != null ? gameObj.transform != null : false) && (MapView.MapIsEnabled || HighLogic.LoadedScene == GameScenes.TRACKSTATION))
            {
                gameObj.transform.position = updateOrbit.getTruePositionAtUT(Planetarium.GetUniversalTime());
                gameObj.transform.localRotation = referenceRotation;
                Vector3 scaled_pos = ScaledSpace.LocalToScaledSpace(gameObj.transform.localPosition);

                //Determine the scale of the line so its thickness is constant from the map camera view
                float apparent_size = 0.01f;
                bool pointed = true;
                switch (info.state)
                {
                    case State.ACTIVE:
                        apparent_size = 0.015f;
                        pointed = true;
                        break;

                    case State.INACTIVE:
                        apparent_size = 0.01f;
                        pointed = true;
                        break;

                    case State.DEAD:
                        apparent_size = 0.01f;
                        pointed = false;
                        break;

                }

                float scale = (float)(apparent_size * Vector3.Distance(MapView.MapCamera.transform.position, scaled_pos));

                //Set line vertex positions
                //needs world direction

                Vector3 line_half_dir = Vector3.one * (scale * ScaledSpace.ScaleFactor);

                if (pointed)
                {
                    line.SetWidth(scale, 0);
                }
                else
                {
                    line.SetWidth(scale, scale);
                    line_half_dir *= 0.5f;
                }

                line.SetPosition(0, ScaledSpace.LocalToScaledSpace(gameObj.transform.localPosition - line_half_dir));
                line.SetPosition(1, ScaledSpace.LocalToScaledSpace(gameObj.transform.localPosition + line_half_dir));
            }
        }

        public void updateRenderProperties(bool force_hide = false)
        {
            try
            {
                if (orbitRenderer == null)
                {
                    return;
                }
                if (gameObj == null)
                {
                    return;
                }

                line.enabled = !force_hide && MapView.MapIsEnabled;

                if (!force_hide && shouldShowOrbit)
                {
                    orbitRenderer.drawMode = OrbitRenderer.DrawMode.REDRAW_AND_RECALCULATE;
                }
                else
                {
                    orbitRenderer.drawMode = OrbitRenderer.DrawMode.OFF;
                }

                //Determine the color
                Color color = activeColor;

                if (orbitRenderer.mouseOver)
                {
                    color = Color.white; //Change line color when moused over
                }
                else
                {
				
                    switch (info.state)
                    {
                        case State.ACTIVE:
                            color = activeColor;
                            break;

                        case State.INACTIVE:
                            color = activeColor * 0.75f;
                            color.a = 1;
                            break;

                        case State.DEAD:
                            color = activeColor * 0.5f;
                            break;
                    }
				
                }

                line.SetColors(color, color);
                orbitRenderer.orbitColor = color * 0.5f;

                if (force_hide || !orbitValid)
                {
                    orbitRenderer.drawIcons = OrbitRenderer.DrawIcons.NONE;
                }
                else
                {
                    if (info.state == State.ACTIVE && shouldShowOrbit)
                    {
                        orbitRenderer.drawIcons = OrbitRenderer.DrawIcons.OBJ_PE_AP;
                    }
                    else
                    {
                        orbitRenderer.drawIcons = OrbitRenderer.DrawIcons.OBJ;
                    }
                }
            }
            catch (Exception e)
            {
                KMP.Log.Debug("Something bad happened: " + e.Message);
            }
        }

        public static bool situationIsGrounded(Situation situation)
        {

            switch (situation)
            {

                case Situation.LANDED:
                case Situation.SPLASHED:
                case Situation.PRELAUNCH:
                case Situation.DESTROYED:
                case Situation.UNKNOWN:
                    return true;

                default:
                    return false;
            }
        }

        public static bool situationIsOrbital(Situation situation)
        {

            switch (situation)
            {

                case Situation.ASCENDING:
                case Situation.DESCENDING:
                case Situation.ENCOUNTERING:
                case Situation.ESCAPING:
                case Situation.ORBITING:
                    return true;

                default:
                    return false;
            }
        }
    }
}
