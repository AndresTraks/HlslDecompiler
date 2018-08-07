vs_3_0
dcl_position v0
dcl_position o0
dcl_position1 o1.xy
mul r0, c1.xyxy, v0.yyxx
mad o0, c0.xyxy, v0.xxyy, r0
mul r0.xy, c1.xy, v0.xx_abs
mad o1.xy, c0.xy, v0.yy_abs, r0.xy
