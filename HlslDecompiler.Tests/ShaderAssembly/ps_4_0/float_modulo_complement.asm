ps_4_0
dcl_constantbuffer CB0[1], immediateIndexed
dcl_input_ps linear v0.xy
dcl_output o0
dcl_temps 1
div r0.xy, |v0.xy|, cb0[0].xy
ge r0.zw, r0.xy, -r0.xy
frc r0.xy, |r0.xy|
movc r0.xy, r0.zw, r0.xy, -r0.xy
mul r0.zw, r0.xy, cb0[0].xy
mad r0.xy, -r0.xy, cb0[0].xy, cb0[0].xy
min r0.xy, r0.xy, r0.zw
min r0.x, r0.y, r0.x
div_sat r0.x, r0.x, cb0[0].z
add o0, -r0.x, l(1, 1, 1, 1)
ret
