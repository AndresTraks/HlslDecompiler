ps_4_0
dcl_immediateConstantBuffer { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } }
dcl_constantbuffer CB0[3], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xy
dcl_input_ps constant v1.x
dcl_output o0
dcl_temps 2
and r0.x, v1.x, l(3)
dp4 r0.x, cb0[1], icb[r0.x]
udiv null, r0.y, cb0[2].x, l(3)
dp3 r0.z, cb0[0].xyz, icb[r0.y].xyz
mul r0.x, r0.z, r0.x
sample r1, v0.xyxx, t0, s0
dp3 r0.y, r1.xyz, icb[r0.y].xyz
mov o0.w, r1.w
mul o0.xyz, r0.xxx, r0.yyy
ret
