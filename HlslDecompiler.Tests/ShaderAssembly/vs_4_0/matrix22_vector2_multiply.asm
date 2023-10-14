vs_4_0
dcl_constantbuffer cb0[2], immediateIndexed
dcl_input v0.xy
dcl_output o0.xyzw
dcl_output o1.xy
dcl_output o1.zw
dcl_temps 1
mul r0.xyzw, v0.yyxx, cb0[1].xyxy
mad o0.xyzw, cb0[0].xyxy, v0.xxyy, r0.xyzw
add r0.xy, v0.xyxx, v0.xyxx
mul r0.yz, r0.yyyy, cb0[1].xxyx
mad o1.zw, cb0[0].xxxy, r0.xxxx, r0.yyyz
mul r0.xy, |v0.xxxx|, cb0[1].xyxx
mad o1.xy, cb0[0].xyxx, |v0.yyyy|, r0.xyxx
ret
