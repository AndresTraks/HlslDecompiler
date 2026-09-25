ds_5_0
dcl_input_control_point_count 4
dcl_tessellator_domain domain_quad
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[6], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input vDomain.xy
dcl_input vicp[4][0].xyz
dcl_input vicp[4][1].xy
dcl_output_siv o0, position
dcl_output o1.xy
dcl_output o1.z
dcl_temps 2
add r0.xy, -vicp[3][1].xy, vicp[2][1].xy
mad r0.xy, vDomain.xx, r0.xy, vicp[3][1].xy
add r0.zw, -vicp[0][1].xy, vicp[1][1].xy
mad r0.zw, vDomain.xx, r0.zw, vicp[0][1].xy
add r0.xy, -r0.zw, r0.xy
mad r0.xy, vDomain.yy, r0.xy, r0.zw
sample_l_indexable(texture2d)(float,float,float,float) r0.z, r0.x, t0.x, s0, l(0)
mov o1.xy, r0.xy
add r0.xyw, -vicp[3][0].xyz, vicp[2][0].xyz
mad r0.xyw, vDomain.xxx, r0.xyw, vicp[3][0].xyz
add r1.xyz, -vicp[0][0].xyz, vicp[1][0].xyz
mad r1.xyz, vDomain.xxx, r1.xyz, vicp[0][0].xyz
add r0.xyw, r0.xyw, -r1.xyz
mad r1.xyz, vDomain.yyy, r0.xyw, r1.xyz
mad r1.y, r0.z, cb0[5].x, r1.y
mov r1.w, l(1)
dp4 o0.x, r1, cb0[0]
dp4 o0.y, r1, cb0[1]
dp4 o0.z, r1, cb0[2]
dp4 o0.w, r1, cb0[3]
add r0.xyz, r1.xyz, -cb0[4].xyz
dp3 r0.x, r0.xyz, r0.xyz
sqrt r0.x, r0.x
div_sat o1.z, r0.x, cb0[4].w
ret
