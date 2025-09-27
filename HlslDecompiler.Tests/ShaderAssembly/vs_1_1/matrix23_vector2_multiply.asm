vs_1_1
dcl_position v0
mul r0, v0.yyxx, c1.xyxy
mad oPos, c0.xyxy, v0.xxyy, r0
max r0.xy, -v0.yx, v0.yx
mul r0.yz, r0.yy, c1.xy
mad oT0.xy, c0.xy, r0.xx, r0.yz
add r0.xy, v0.xy, v0.xy
mul r0.yz, r0.yy, c1.xy
mad oT1.xy, c0.xy, r0.xx, r0.yz
