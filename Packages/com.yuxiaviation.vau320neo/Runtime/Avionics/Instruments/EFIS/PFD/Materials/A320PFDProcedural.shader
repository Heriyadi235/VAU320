Shader "VAU320/EFIS/A320 PFD Procedural"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture", 2D) = "white" {}
        _BackgroundTex ("A320 PFD Background", 2D) = "black" {}
        _AttitudeMaskTex ("PFD Attitude Mask", 2D) = "white" {}
        _UseTextureMask ("Use Texture Attitude Mask", Range(0, 1)) = 0
        _MaskThreshold ("Attitude Mask Threshold", Range(0, 1)) = 0.5
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _SkyColor ("Sky Color", Color) = (0.02, 0.33, 0.55, 1)
        _GroundColor ("Ground Color", Color) = (0.48, 0.24, 0.09, 1)
        _ReferenceColor ("Reference Color", Color) = (1, 0.82, 0.05, 1)
        _FDColor ("Flight Director Color", Color) = (1, 0.05, 0.75, 1)
        _LineColor ("Line Color", Color) = (1, 1, 1, 1)
        _WarningColor ("Warning Color", Color) = (1, 0.55, 0.05, 1)

        _Pitch ("Pitch Degrees", Float) = 0
        _Bank ("Bank Degrees", Float) = 0
        _Airspeed ("Airspeed", Range(0, 400)) = 140
        _Altitude ("Altitude", Range(0, 50000)) = 10000
        _VerticalSpeed ("Vertical Speed", Range(-6000, 6000)) = 0
        _Heading ("Heading Degrees", Range(0, 360)) = 0
        _Slip ("Slip", Range(-1, 1)) = 0
        _ShowFlightDirector ("Show Flight Director", Range(0, 1)) = 1
        _FDHorizontal ("FD Horizontal", Range(-1, 1)) = 0
        _FDVertical ("FD Vertical", Range(-1, 1)) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _BackgroundTex;
            sampler2D _AttitudeMaskTex;
            fixed4 _Color;
            fixed4 _SkyColor;
            fixed4 _GroundColor;
            fixed4 _ReferenceColor;
            fixed4 _FDColor;
            fixed4 _LineColor;
            fixed4 _WarningColor;
            float _Pitch;
            float _Bank;
            float _Airspeed;
            float _Altitude;
            float _VerticalSpeed;
            float _Heading;
            float _Slip;
            float _ShowFlightDirector;
            float _FDHorizontal;
            float _FDVertical;
            float _UseTextureMask;
            float _MaskThreshold;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            float2 rotatePoint(float2 samplePoint, float angleDegrees)
            {
                float radians = angleDegrees * 0.0174532925;
                float sine = sin(radians);
                float cosine = cos(radians);
                return float2(cosine * samplePoint.x - sine * samplePoint.y,
                              sine * samplePoint.x + cosine * samplePoint.y);
            }

            float lineMask(float2 samplePoint, float2 startPoint, float2 endPoint, float width)
            {
                float2 segment = endPoint - startPoint;
                float2 offset = samplePoint - startPoint;
                float segmentLength = max(dot(segment, segment), 0.000001);
                float amount = saturate(dot(offset, segment) / segmentLength);
                float distanceToLine = length(offset - segment * amount);
                float antialias = max(fwidth(distanceToLine), 0.0005);
                return 1 - smoothstep(width, width + antialias, distanceToLine);
            }

            float rectangleMask(float2 samplePoint, float2 minimum, float2 maximum)
            {
                float insideX = step(minimum.x, samplePoint.x) * step(samplePoint.x, maximum.x);
                float insideY = step(minimum.y, samplePoint.y) * step(samplePoint.y, maximum.y);
                return insideX * insideY;
            }

            float tapeTick(float2 samplePoint, float centerX, float normalizedValue, float tickIndex)
            {
                float tickY = normalizedValue + tickIndex * 0.045;
                float tickLength = (fmod(abs(tickIndex), 2) < 0.5) ? 0.035 : 0.018;
                return lineMask(samplePoint, float2(centerX, tickY), float2(centerX + tickLength, tickY), 0.0015);
            }

            fixed4 drawColor(fixed4 baseColor, fixed4 overlayColor, float mask)
            {
                return lerp(baseColor, overlayColor, saturate(mask) * overlayColor.a);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 samplePoint = i.uv - 0.5;
                fixed4 textureColor = tex2D(_MainTex, i.uv);
                fixed4 backgroundColor = tex2D(_BackgroundTex, i.uv);
                fixed4 baseColor = backgroundColor * textureColor * i.color;
                float rectangleAttitudeMask = rectangleMask(samplePoint, float2(-0.364, -0.288), float2(0.242, 0.256));
                float textureAttitudeMask = tex2D(_AttitudeMaskTex, i.uv).a;
                textureAttitudeMask = smoothstep(_MaskThreshold - 0.02, _MaskThreshold + 0.02, textureAttitudeMask);
                float attitudeWindow = lerp(rectangleAttitudeMask, textureAttitudeMask, _UseTextureMask);
                float2 attitudePoint = rotatePoint(samplePoint - float2(-0.061, -0.016), _Bank);
                float pitchOffset = _Pitch * 0.008;
                float horizon = attitudePoint.y + pitchOffset;
                float horizonBlend = smoothstep(-0.002, 0.002, horizon);
                fixed4 outputColor = lerp(_GroundColor, _SkyColor, horizonBlend);

                float horizonLine = lineMask(attitudePoint, float2(-0.34, -pitchOffset), float2(0.34, -pitchOffset), 0.0015);
                outputColor = drawColor(outputColor, _LineColor, horizonLine);

                for (int pitchTick = -3; pitchTick <= 3; pitchTick++)
                {
                    if (pitchTick != 0)
                    {
                        float tickY = -pitchOffset + pitchTick * 0.08;
                        float tickWidth = (pitchTick % 2 == 0) ? 0.11 : 0.075;
                        float leftTick = lineMask(attitudePoint, float2(-0.035 - tickWidth, tickY), float2(-0.035, tickY), 0.0015);
                        float rightTick = lineMask(attitudePoint, float2(0.035, tickY), float2(0.035 + tickWidth, tickY), 0.0015);
                        outputColor = drawColor(outputColor, _LineColor, max(leftTick, rightTick));
                    }
                }

                float2 bankPoint = samplePoint - float2(-0.061, -0.016);
                for (int bankTickIndex = -6; bankTickIndex <= 6; bankTickIndex++)
                {
                    if (bankTickIndex != 0)
                    {
                        float bankAngle = bankTickIndex * 10.0 * 0.0174532925;
                        float2 bankStart = float2(sin(bankAngle), cos(bankAngle)) * 0.245;
                        float2 bankEnd = float2(sin(bankAngle), cos(bankAngle)) * 0.265;
                        float bankTick = lineMask(bankPoint, bankStart, bankEnd, 0.0018);
                        outputColor = drawColor(outputColor, _LineColor, bankTick);
                    }
                }
                float bankIndex = lineMask(bankPoint, float2(0, 0.255), float2(0, 0.285), 0.0025);
                outputColor = drawColor(outputColor, _ReferenceColor, bankIndex);

                float aircraftLeft = lineMask(attitudePoint, float2(-0.19, 0), float2(-0.055, 0), 0.003);
                float aircraftRight = lineMask(attitudePoint, float2(0.055, 0), float2(0.19, 0), 0.003);
                float aircraftTipLeft = lineMask(attitudePoint, float2(-0.055, 0), float2(-0.055, -0.025), 0.003);
                float aircraftTipRight = lineMask(attitudePoint, float2(0.055, 0), float2(0.055, -0.025), 0.003);
                outputColor = drawColor(outputColor, _ReferenceColor, max(max(aircraftLeft, aircraftRight), max(aircraftTipLeft, aircraftTipRight)));

                float airspeedPanel = rectangleMask(attitudePoint, float2(-0.49, -0.38), float2(-0.37, 0.38));
                float altitudePanel = rectangleMask(attitudePoint, float2(0.37, -0.38), float2(0.49, 0.38));
                float panelBorder = lineMask(attitudePoint, float2(-0.37, -0.38), float2(-0.37, 0.38), 0.001);
                panelBorder = max(panelBorder, lineMask(attitudePoint, float2(0.37, -0.38), float2(0.37, 0.38), 0.001));
                outputColor = drawColor(outputColor, _LineColor, max(panelBorder, (1 - airspeedPanel) * 0));
                outputColor = drawColor(outputColor, _LineColor, max(panelBorder, (1 - altitudePanel) * 0));

                float airspeedNormalized = ((_Airspeed - 140) / 80) * 0.22;
                float altitudeNormalized = ((_Altitude - 10000) / 10000) * 0.22;
                for (int tapeIndex = -4; tapeIndex <= 4; tapeIndex++)
                {
                    float airspeedTick = tapeTick(attitudePoint, -0.445, airspeedNormalized, tapeIndex) * airspeedPanel;
                    float altitudeTick = tapeTick(attitudePoint, 0.405, altitudeNormalized, tapeIndex) * altitudePanel;
                    outputColor = drawColor(outputColor, _LineColor, max(airspeedTick, altitudeTick));
                }

                float verticalSpeedNormalized = saturate(_VerticalSpeed / 6000) * 0.25;
                float vsNeedle = lineMask(attitudePoint, float2(0.31, 0), float2(0.31, verticalSpeedNormalized), 0.002);
                outputColor = drawColor(outputColor, abs(_VerticalSpeed) > 6000 ? _WarningColor : _ReferenceColor, vsNeedle);

                float headingAngle = (_Heading - 180) * 0.0025;
                float headingNeedle = lineMask(attitudePoint, float2(headingAngle, -0.42), float2(headingAngle, -0.34), 0.002);
                outputColor = drawColor(outputColor, _ReferenceColor, headingNeedle);

                float slipIndicator = lineMask(attitudePoint, float2(_Slip * 0.08, -0.05), float2(_Slip * 0.08, 0.05), 0.002);
                outputColor = drawColor(outputColor, _ReferenceColor, slipIndicator);

                float2 fdCenter = float2(_FDHorizontal * 0.18, _FDVertical * 0.18);
                float fdHorizontal = lineMask(attitudePoint, fdCenter + float2(-0.08, 0), fdCenter + float2(0.08, 0), 0.002);
                float fdVertical = lineMask(attitudePoint, fdCenter + float2(0, -0.08), fdCenter + float2(0, 0.08), 0.002);
                outputColor = drawColor(outputColor, _FDColor, (fdHorizontal + fdVertical) * _ShowFlightDirector);

                fixed4 finalColor = lerp(baseColor, outputColor, attitudeWindow);

                float iasPanel = rectangleMask(samplePoint, float2(-0.500, -0.283), float2(-0.344, 0.251));
                float iasReference = lineMask(samplePoint, float2(-0.500, 0.000), float2(-0.482, 0.000), 0.002);
                for (int iasTickIndex = -5; iasTickIndex <= 5; iasTickIndex++)
                {
                    float iasTick = tapeTick(samplePoint, -0.480, airspeedNormalized, iasTickIndex) * iasPanel;
                    finalColor = drawColor(finalColor, _LineColor, iasTick);
                }
                finalColor = drawColor(finalColor, _ReferenceColor, iasReference * iasPanel);

                float vsiPanel = rectangleMask(samplePoint, float2(0.419, -0.328), float2(0.482, 0.298));
                float vsiCenter = lineMask(samplePoint, float2(0.419, 0.000), float2(0.445, 0.000), 0.0015);
                float vsiNeedle = lineMask(samplePoint, float2(0.451, 0.000), float2(0.451, verticalSpeedNormalized), 0.002);
                finalColor = drawColor(finalColor, _LineColor, vsiCenter * vsiPanel);
                finalColor = drawColor(finalColor, _ReferenceColor, vsiNeedle * vsiPanel);

                float altitudeBandPanel = rectangleMask(samplePoint, float2(0.228, -0.283), float2(0.404, 0.251));
                for (int altitudeTickIndex = -5; altitudeTickIndex <= 5; altitudeTickIndex++)
                {
                    float altitudeTick = tapeTick(samplePoint, 0.245, altitudeNormalized, altitudeTickIndex) * altitudeBandPanel;
                    finalColor = drawColor(finalColor, _LineColor, altitudeTick);
                }
                float altitudeReference = lineMask(samplePoint, float2(0.386, 0.000), float2(0.404, 0.000), 0.002);
                finalColor = drawColor(finalColor, _ReferenceColor, altitudeReference * altitudeBandPanel);

                float headingPanel = rectangleMask(samplePoint, float2(-0.289, -0.489), float2(0.172, -0.405));
                float headingCenter = lineMask(samplePoint, float2(-0.004, -0.489), float2(-0.004, -0.468), 0.002);
                for (int headingTickIndex = -6; headingTickIndex <= 6; headingTickIndex++)
                {
                    float headingTickX = -0.004 + headingTickIndex * 0.045;
                    float headingTick = lineMask(samplePoint, float2(headingTickX, -0.489), float2(headingTickX, -0.469), 0.0015) * headingPanel;
                    finalColor = drawColor(finalColor, _LineColor, headingTick);
                }
                finalColor = drawColor(finalColor, _ReferenceColor, headingCenter * headingPanel);

                finalColor.a *= i.color.a;
                return finalColor;
            }
            ENDCG
        }
    }
}
