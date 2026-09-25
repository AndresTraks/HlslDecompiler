ds_5_0
dcl_input_control_point_count 3
dcl_tessellator_domain domain_tri
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[5], immediateIndexed
dcl_sampler s0, mode_default
dcl_resource_texture2d (float,float,float,float) t0
dcl_input vpc0.yzw
dcl_input vpc1.y
dcl_input vDomain.xyz
dcl_input vicp[3][0].xyz
dcl_input vicp[3][1].xyz
dcl_input vicp[3][2].xy
dcl_output_siv o0, position
dcl_output o1.xyz
dcl_output o2.xy
dcl_temps 3
mul r0.xyz, vDomain.yyy, vicp[1][0].xyz
mad r0.xyz, vicp[0][0].xyz, vDomain.xxx, r0.xyz
mad r0.xyz, vicp[2][0].xyz, vDomain.zzz, r0.xyz
mul r1.xyz, vDomain.yyy, vicp[1][1].xyz
mad r1.xyz, vicp[0][1].xyz, vDomain.xxx, r1.xyz
mad r1.xyz, vicp[2][1].xyz, vDomain.zzz, r1.xyz
dp3 r0.w, r1.xyz, r1.xyz
rsq r0.w, r0.w
mul r1.xyz, r0.www, r1.xyz
mul r2.xy, vDomain.yy, vicp[1][2].xy
mad r2.xy, vicp[0][2].xy, vDomain.xx, r2.xy
mad r2.xy, vicp[2][2].xy, vDomain.zz, r2.xy
sample_l_indexable(texture2d)(float,float,float,float) r0.w, r2.x, t0.x, s0, l(0)
mov o2.xy, r2.xy
mul r0.w, r0.w, cb0[4].x
mul r0.w, r0.w, vpc1.y
mad r0.xyz, r1.xyz, r0.www, r0.xyz
mov o1.xyz, r1.xyz
add r1.xyz, -r0.xyz, vpc0.yzw
mad r0.xyz, r1.xyz, l(0.00999999978, 0.00999999978, 0.00999999978, 0), r0.xyz
mov r0.w, l(1)
dp4 o0.x, r0, cb0[0]
dp4 o0.y, r0, cb0[1]
dp4 o0.z, r0, cb0[2]
dp4 o0.w, r0, cb0[3]
ret
