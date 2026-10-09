hs_5_0
hs_decls
dcl_input_control_point_count 3
dcl_output_control_point_count 3
dcl_tessellator_domain domain_tri
dcl_tessellator_partitioning partitioning_integer
dcl_tessellator_output_primitive output_triangle_cw
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[5], immediateIndexed
hs_control_point_phase
dcl_input vOutputControlPointID
dcl_input v[3][0].xyz
dcl_input v[3][1].xyz
dcl_output o0.xyz
dcl_output o0.w
dcl_output o1.xyz
dcl_temps 3
mov r0.w, l(1)
mov r1.x, vOutputControlPointID
mov r0.xyz, v[r1.x][0].xyz
dp4 r2.x, r0, cb0[0]
dp4 r2.y, r0, cb0[1]
dp4 r2.z, r0, cb0[2]
dp3 r0.x, r2.xyz, r2.xyz
sqrt r0.x, r0.x
max r0.x, r0.x, l(0.00999999978)
div o0.w, l(1, 1, 1, 1), r0.x
mov o0.xyz, v[r1.x][0].xyz
dp3 r0.x, v[r1.x][1].xyz, v[r1.x][1].xyz
rsq r0.x, r0.x
mul o1.xyz, r0.xxx, v[r1.x][1].xyz
ret
hs_join_phase
dcl_input vocp[3][0].w
dcl_output_siv o0.x, finalTriUeq0EdgeTessFactor
dcl_output_siv o1.x, finalTriVeq0EdgeTessFactor
dcl_output_siv o2.x, finalTriWeq0EdgeTessFactor
dcl_output_siv o3.x, finalTriInsideTessFactor
dcl_temps 1
add r0.x, vocp[1][0].w, vocp[0][0].w
add r0.x, r0.x, vocp[2][0].w
mul r0.x, r0.x, cb0[4].x
mul r0.x, r0.x, l(0.333333343)
max r0.x, r0.x, l(1)
mov o0.x, r0.x
mov o1.x, r0.x
mov o2.x, r0.x
mov o3.x, r0.x
ret
