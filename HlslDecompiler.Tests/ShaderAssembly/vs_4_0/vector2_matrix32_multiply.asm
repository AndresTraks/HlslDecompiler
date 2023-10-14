vs_4_0
dcl_constantbuffer cb0[2], immediateIndexed
dcl_input v0.xy
dcl_output o0.xyzw
dcl_output o1.xy
dcl_output o1.zw
dcl_temps 1
dp2 o0.x, v0.xyxx, cb0[0].xyxx
dp2 o0.y, v0.xyxx, cb0[1].xyxx
dp2 o0.z, v0.yxyy, cb0[0].xyxx
dp2 o0.w, v0.yxyy, cb0[1].xyxx
dp2 o1.x, |v0.yxyy|, cb0[0].xyxx
dp2 o1.y, |v0.yxyy|, cb0[1].xyxx
add r0.xy, v0.xyxx, v0.xyxx
dp2 o1.z, r0.xyxx, cb0[0].xyxx
dp2 o1.w, r0.xyxx, cb0[1].xyxx
ret
