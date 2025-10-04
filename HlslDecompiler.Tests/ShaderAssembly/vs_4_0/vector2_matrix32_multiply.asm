vs_4_0
dcl_constantbuffer cb0[2], immediateIndexed
dcl_input v0.xy
dcl_output o0
dcl_output o1.xy
dcl_output o1.zw
dcl_temps 1
dp2 o0.x, v0.xy, cb0[0].xy
dp2 o0.y, v0.xy, cb0[1].xy
dp2 o0.z, v0.yx, cb0[0].xy
dp2 o0.w, v0.yx, cb0[1].xy
dp2 o1.x, |v0.yx|, cb0[0].xy
dp2 o1.y, |v0.yx|, cb0[1].xy
add r0.xy, v0.xy, v0.xy
dp2 o1.z, r0.xy, cb0[0].xy
dp2 o1.w, r0.xy, cb0[1].xy
ret
