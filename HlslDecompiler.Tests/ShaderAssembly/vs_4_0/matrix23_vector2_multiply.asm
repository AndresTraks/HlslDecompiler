vs_4_0
dcl_constantbuffer cb0[2], immediateIndexed
dcl_input v0.xy
dcl_output o0
dcl_output o1
dcl_temps 1
mul r0, v0.yyxx, cb0[1].xyxy
mad o0, cb0[0].xyxy, v0.xxyy, r0
add r0.xy, v0.xy, v0.xy
mul r0.yz, r0.yy, cb0[1].xy
mad o1.zw, cb0[0].xy, r0.xx, r0.yz
mul r0.xy, |v0.xx|, cb0[1].xy
mad o1.xy, cb0[0].xy, |v0.yy|, r0.xy
ret
