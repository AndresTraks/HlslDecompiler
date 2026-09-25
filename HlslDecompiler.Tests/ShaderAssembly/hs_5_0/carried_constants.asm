hs_5_0
hs_decls
dcl_input_control_point_count 3
dcl_output_control_point_count 3
dcl_tessellator_domain domain_tri
dcl_tessellator_partitioning partitioning_fractional_odd
dcl_tessellator_output_primitive output_triangle_cw
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
hs_control_point_phase
dcl_input vOutputControlPointID
dcl_input v[3][0].xyz
dcl_input v[3][1].xyz
dcl_output o0.xyz
dcl_output o1.xyz
dcl_temps 2
mov r0.x, vOutputControlPointID
add r0.yzw, cb0[0].xyz, -v[r0.x][0].xyz
dp3 r1.x, r0.yzw, r0.yzw
rsq r1.x, r1.x
mul r0.yzw, r0.yzw, r1.xxx
dp3 r1.x, r0.yzw, v[r0.x][1].xyz
mad r0.yzw, r0.yzw, l(0, 0.25, 0.25, 0.25), v[r0.x][1].xyz
mul r1.xyz, r1.xxx, v[r0.x][1].xyz
mad o0.xyz, r1.xyz, l(0.100000001, 0.100000001, 0.100000001, 0), v[r0.x][0].xyz
dp3 r0.x, r0.yzw, r0.yzw
rsq r0.x, r0.x
mul o1.xyz, r0.xxx, r0.yzw
ret
hs_fork_phase
dcl_input vicp[3][0].x
dcl_output o0.y
dcl_temps 1
add r0.x, vicp[1][0].x, vicp[0][0].x
add r0.x, r0.x, vicp[2][0].x
mul o0.y, r0.x, l(0.333333343)
ret
hs_fork_phase
dcl_input vicp[3][0].y
dcl_output o0.z
dcl_temps 1
add r0.x, vicp[1][0].y, vicp[0][0].y
add r0.x, r0.x, vicp[2][0].y
mul o0.z, r0.x, l(0.333333343)
ret
hs_fork_phase
dcl_input vicp[3][0].z
dcl_output o0.w
dcl_temps 1
add r0.x, vicp[1][0].z, vicp[0][0].z
add r0.x, r0.x, vicp[2][0].z
mul o0.w, r0.x, l(0.333333343)
ret
hs_join_phase
dcl_input vpc0.yzw
dcl_input vicp[3][0].xyz
dcl_output_siv o0.x, finalTriUeq0EdgeTessFactor
dcl_output_siv o1.x, finalTriVeq0EdgeTessFactor
dcl_output o1.y
dcl_output_siv o2.x, finalTriWeq0EdgeTessFactor
dcl_output_siv o3.x, finalTriInsideTessFactor
dcl_temps 1
add r0.xyz, -cb0[0].xyz, vpc0.yzw
dp3 r0.x, r0.xyz, r0.xyz
sqrt r0.x, r0.x
max r0.x, r0.x, l(1)
add r0.yzw, -vicp[0][0].xyz, vpc0.yzw
dp3 r0.y, r0.yzw, r0.yzw
sqrt r0.y, r0.y
mul r0.z, r0.y, cb0[0].w
mov o1.y, r0.y
div r0.x, r0.z, r0.x
mov o0.x, r0.x
mov o1.x, r0.x
mov o2.x, r0.x
mul o3.x, r0.x, l(0.5)
ret
