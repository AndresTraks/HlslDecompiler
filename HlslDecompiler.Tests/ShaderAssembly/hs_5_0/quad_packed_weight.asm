hs_5_0
hs_decls
dcl_input_control_point_count 4
dcl_output_control_point_count 4
dcl_tessellator_domain domain_quad
dcl_tessellator_partitioning partitioning_integer
dcl_tessellator_output_primitive output_triangle_cw
dcl_globalFlags refactoringAllowed
dcl_constantbuffer CB0[1], immediateIndexed
hs_control_point_phase
dcl_input vOutputControlPointID
dcl_input v[4][0].xyz
dcl_input v[4][0].w
dcl_output o0.xyz
dcl_output o0.w
dcl_temps 1
mov r0.x, vOutputControlPointID
mul o0, l(2, 2, 2, 1), v[r0.x][0]
ret
hs_fork_phase
dcl_hs_fork_phase_instance_count 4
dcl_input vForkInstanceID
dcl_input vicp[4][0].w
dcl_output_siv o0.x, finalQuadUeq0EdgeTessFactor
dcl_output_siv o1.x, finalQuadVeq0EdgeTessFactor
dcl_output_siv o2.x, finalQuadUeq1EdgeTessFactor
dcl_output_siv o3.x, finalQuadVeq1EdgeTessFactor
dcl_temps 1
dcl_indexrange o0 4
iadd r0.x, vForkInstanceID.x, l(1)
and r0.x, r0.x, l(3)
mov r0.y, vForkInstanceID.x
max r0.x, vicp[r0.x][0].w, vicp[r0.y][0].w
mul r0.x, r0.x, cb0[0].x
mov o[r0.y].x, r0.x
ret
hs_join_phase
dcl_hs_join_phase_instance_count 2
dcl_input vpc0.x
dcl_input vpc1.x
dcl_input vpc2.x
dcl_input vpc3.x
dcl_input vJoinInstanceID
dcl_output_siv o4.x, finalQuadUInsideTessFactor
dcl_output_siv o5.x, finalQuadVInsideTessFactor
dcl_temps 1
dcl_indexrange o4 2
dcl_indexrange vpc0 4
mov r0.x, vJoinInstanceID.x
add r0.y, vpc[r0.x].x, vpc[r0.x + 2].x
mul r0.y, r0.y, l(0.5)
mov o[r0.x + 4].x, r0.y
ret
