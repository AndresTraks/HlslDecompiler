ps_4_0
dcl_constantbuffer cb0[1], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input_ps linear v0.xyz
dcl_input_ps linear v1.xyz
dcl_output o0
dcl_temps 2
add r0.xyz, v1.xyz, -cb0[0].xyz
dp3 r0.w, r0.xyz, r0.xyz
rsq r0.w, r0.w
mul r0.xyz, r0.www, r0.xyz
dp3 r0.w, v0.xyz, v0.xyz
rsq r0.w, r0.w
mul r1.xyz, r0.www, v0.xyz
dp3 r0.w, r0.xyz, r1.xyz
add r0.w, r0.w, r0.w
mad r0.xyz, r1.xyz, -r0.www, r0.xyz
sample r0, r0.xyzx, t0, s0
mul o0, r0, cb0[0].w
ret
